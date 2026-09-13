using System.Net.WebSockets;
using Serilog;
using CoverageManager.Core.Engines;
using CoverageManager.Api.Services;
using CoverageManager.Api.Workers;
using CoverageManager.Connector;

// Default to Warning; quiet the frameworks that spam per-request. Keep our own
// domain logs at Information. Console I/O is the biggest source of thread-starvation
// on this service (collector POSTs 10/sec, Kestrel logs 5 lines per request).
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .MinimumLevel.Override("CoverageManager", Serilog.Events.LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", Serilog.Events.LogEventLevel.Information)
    .MinimumLevel.Override("System.Net.Http", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Async(a => a.Console())
    .WriteTo.Async(a => a.File("logs/coverage-manager-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30))
    .CreateLogger();

// Prevent thread-pool starvation under burst load. The Python collector POSTs
// /api/coverage/positions at 10 req/sec; default MinThreads ramps slowly, so
// pre-size the pool. 200 is generous; system caps IOCP anyway.
System.Threading.ThreadPool.SetMinThreads(200, 200);

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // Core singletons
    var positionManager = new PositionManager();
    var priceCache = new PriceCache();
    // PriceCache injected so floating P&L is recomputed from live ticks
    // (every WS frame) instead of stale Position.Profit (60s poll only).
    var exposureEngine = new ExposureEngine(positionManager, priceCache);
    var dealStore = new DealStore();

    var alertEngine = new AlertEngine(exposureEngine, positionManager);

    builder.Services.AddSingleton(positionManager);
    builder.Services.AddSingleton(priceCache);
    builder.Services.AddSingleton(exposureEngine);
    builder.Services.AddSingleton(dealStore);
    builder.Services.AddSingleton(alertEngine);

    // Single IHttpClientFactory for every outbound HTTP caller. Each service
    // uses its class name as the logical client name so Polly/retry policies
    // can be attached per-service later without changing the services themselves.
    builder.Services.AddHttpClient();

    // Supabase:ReadOnly = true blocks every write to Supabase at the HTTP layer, on every factory
    // client. As of the bridge_executions port there is NO Supabase writer left in v2 -- the
    // whole domain store is Postgres -- so this is now a belt-and-braces backstop that would
    // catch any future code that reaches for Supabase by mistake. The ledger of blocked writes
    // is on /api/exposure/diagnostics.supabaseReadOnly.
    var supabaseReadOnly = builder.Configuration.GetValue("Supabase:ReadOnly", false);
    var supabaseHost = Uri.TryCreate(builder.Configuration["Supabase:Url"], UriKind.Absolute, out var supabaseUri) ? supabaseUri.Host : "";
    builder.Services.AddSingleton(new SupabaseReadOnlyLedger(supabaseReadOnly, supabaseHost));
    if (supabaseReadOnly)
    {
        builder.Services.AddTransient<SupabaseReadOnlyHandler>();
        builder.Services.ConfigureHttpClientDefaults(http => http.AddHttpMessageHandler<SupabaseReadOnlyHandler>());
        Log.Warning("Supabase READ-ONLY mode: every write to {Host} is blocked (Supabase:ReadOnly = true)", supabaseHost);
    }

    // v2 data store: direct Npgsql to the local coverage_v2 PostgreSQL database.
    // Replaces the Supabase PostgREST HTTP client. Everything depends on the
    // IDataStore interface so the store is swapped by this registration alone;
    // SupabaseService still implements IDataStore and can be re-registered here
    // if a PostgREST fallback is ever needed.
    builder.Services.AddSingleton<PostgresService>(sp =>
        new PostgresService(
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<ILogger<PostgresService>>()));
    builder.Services.AddSingleton<IDataStore>(sp => sp.GetRequiredService<PostgresService>());

    // Broadcast service (WebSocket push)
    builder.Services.AddSingleton<ExposureBroadcastService>();

    // MT5 API provider. v2 has exactly one: the Live Bridge consumer feed. Normalized here so
    // a stale "Manager" value fails at startup with a migration message instead of looping
    // inside the reconnect backoff.
    var mt5Provider = MT5ApiProviders.Normalize(builder.Configuration["MT5:Provider"]);
    var liveBridgeOptions = builder.Configuration.GetSection(LiveBridgeOptions.SectionName).Get<LiveBridgeOptions>()
                            ?? new LiveBridgeOptions();
    Log.Information("MT5 API provider: {Provider}", mt5Provider);
    builder.Services.AddSingleton<IMT5ApiFactory>(sp =>
        new MT5ApiFactory(mt5Provider, liveBridgeOptions, sp.GetRequiredService<ILoggerFactory>()));

    // B-Book feed connection (Live Bridge). Keeps the historical class name; under the feed
    // it never reads account_settings -- see MT5ApiFactory.RequiresManagerAccount.
    builder.Services.AddSingleton<MT5ManagerConnection>(sp =>
    {
        var supabase = sp.GetRequiredService<IDataStore>();
        var broadcast = sp.GetRequiredService<ExposureBroadcastService>();
        return new MT5ManagerConnection(
            sp.GetRequiredService<ILogger<MT5ManagerConnection>>(),
            positionManager,
            priceCache,
            dealStore,
            async () => await supabase.GetAccountSettingsAsync(),
            () => broadcast.MarkDirty(),
            async accounts => await supabase.UpsertTradingAccountsAsync(accounts),
            async source => await supabase.GetLastDealTimeAsync(source),
            // Fast path: tick events route through the lightweight price-only
            // broadcast (20 Hz) instead of the heavy full-state broadcast
            // (10 Hz, which recomputes exposure across every position). Keeps
            // the bid price under each symbol fresh even when the position
            // book is large.
            onPriceTick: _ => broadcast.MarkPriceDirty(),
            // Phase 2.19: per-deal settled-delta push. Mirrors the formula
            // the `aggregate_bbook_settled_pnl` SQL function uses so the live
            // overlay matches what the next REST refresh will see in Supabase:
            //   delta = (entry IN (1,2,3) ? profit + swap : 0) + commission + fee
            // Canonical key matches the SQL's normalization
            // (UPPER(strip trailing '.xxx' or trailing dashes)) so the
            // frontend overlay lands on the right row.
            onDealSettled: deal =>
            {
                var key = NormalizeCanonicalKey(deal.Symbol);
                if (string.IsNullOrEmpty(key)) return;
                var delta = (deal.Entry >= 1 && deal.Entry <= 3 ? deal.Profit + deal.Swap : 0m)
                          + deal.Commission + deal.Fee;
                broadcast.BroadcastDealSettled(key, delta, deal.Time, deal.DealId, "bbook");
            },
            apiFactory: sp.GetRequiredService<IMT5ApiFactory>());
    });
    builder.Services.AddHostedService(sp => sp.GetRequiredService<MT5ManagerConnection>());

    // MT5 Coverage connection (LP account — reads coverage positions)
    builder.Services.AddSingleton<MT5CoverageConnection>(sp =>
    {
        var supabase = sp.GetRequiredService<IDataStore>();
        var broadcast = sp.GetRequiredService<ExposureBroadcastService>();
        return new MT5CoverageConnection(
            sp.GetRequiredService<ILogger<MT5CoverageConnection>>(),
            positionManager,
            priceCache,
            async () => await supabase.GetAccountSettingsAsync(),
            () => broadcast.MarkDirty(),
            apiFactory: sp.GetRequiredService<IMT5ApiFactory>());
    });
    builder.Services.AddHostedService(sp => sp.GetRequiredService<MT5CoverageConnection>());

    // Data sync service (persists deals to Supabase, detects modifications)
    var dataSyncOptions = builder.Configuration.GetSection(DataSyncOptions.SectionName).Get<DataSyncOptions>() ?? new DataSyncOptions();
    builder.Services.AddSingleton<DataSyncService>(sp =>
        new DataSyncService(
            sp.GetRequiredService<IDataStore>(),
            dealStore,
            positionManager,
            sp.GetRequiredService<ILogger<DataSyncService>>(),
            dataSyncOptions));
    builder.Services.AddHostedService(sp => sp.GetRequiredService<DataSyncService>());

    // ---- Phase 2.5: Bridge Execution Analysis (Centroid Dropcopy feed) ----
    // Pairing window and feed mode are read from config; defaults are safe (Stub + 10s).
    var bridgePairingWindowMs = builder.Configuration.GetValue("Centroid:PairingWindowMs", 10_000);

    builder.Services.AddSingleton<BridgeExecutionStore>(sp =>
        new BridgeExecutionStore(
            bridgePairingWindowMs,
            sp.GetRequiredService<ILogger<BridgeExecutionStore>>()));

    builder.Services.AddSingleton<BridgeBroadcastService>();

    // bridge_executions now goes through IDataStore -> PostgresService like everything else.
    // BridgeSupabaseWriter (its own HTTP client straight to Supabase) was deleted in v2.

    // Live (real Centroid dropcopy) is the only feed implementation in v2 -- the synthetic
    // StubCentroidBridgeService was retired. BridgeFeedHost stays dormant unless Live is
    // explicitly configured and enabled.
    builder.Services.AddSingleton<RestCentroidBridgeService>();
    builder.Services.AddSingleton<BridgeFeedHost>();
    // Controllers & worker depend on ICentroidBridgeService — route that to the host facade.
    builder.Services.AddSingleton<ICentroidBridgeService, BridgeFeedHostAdapter>();

    builder.Services.AddHostedService<BridgeExecutionWorker>();

    // Coverage-side deal index — polls the Python collector's /deals/raw and maps
    // MT5 order_ticket -> MT5 deal_ticket on the 96900 account. Enables deal-per-deal
    // reconciliation on the COV OUT side (Centroid maker_order_id echoes onto 96900 as order_ticket).
    // Uses the typed-client pattern because CoverageDealIndex's constructor takes
    // HttpClient directly (as opposed to IHttpClientFactory). AddHttpClient<T>
    // registers the typed client; the separate AddSingleton promotes it to a
    // singleton scope so the background poll loop survives for the process lifetime.
    builder.Services.AddHttpClient<CoverageDealIndex>();
    builder.Services.AddSingleton<CoverageDealIndex>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<CoverageDealIndex>());

    // Period P&L scheduler — dispatches snapshot_schedules every 60s.
    builder.Services.AddSingleton<ExposureSnapshotService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<ExposureSnapshotService>());

    // Coverage LP account sync — polls the Python collector's /account endpoint
    // every 5 min and upserts the LP's balance/credit/equity to trading_accounts.
    // Needed because MT5 Manager API (used for B-Book) can't see LP accounts.
    builder.Services.AddHostedService<CoverageAccountSyncService>();

    // v2 feed/store self-check (V2_PLAN 5.4). Replaces v1's heavy ReconciliationService sweep
    // (deleted): compares the Live Bridge working set with Postgres for the retained window,
    // re-writes what diverged, never deletes. Backs /api/reconciliation/* unchanged.
    builder.Services.AddSingleton<IFeedDealSource>(sp =>
        new ConnectionFeedDealSource(sp.GetRequiredService<MT5ManagerConnection>()));
    builder.Services.AddSingleton<FeedStoreSelfCheckService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<FeedStoreSelfCheckService>());

    // CashMovementSyncService was RETIRED in v2 Phase 2 (source consolidation).
    // It existed because MT5 Manager's CIMTDealSink didn't fire for admin balance/credit
    // transfers, so a 15-min / 7-day-lookback sweep had to backfill them. The Live Bridge
    // feed pushes admin deals itself, and walking 26,845 feed logins took hours per cycle,
    // so under the feed it did nothing but cost. With the Manager provider gone it could
    // only ever idle. The one-shot Settings action (/api/equity-pnl/backfill-cash-movements)
    // is unaffected and still available.

    // Resilient mapping cache — auto-heals when the cold-start Supabase
    // fetch hits a transient TLS reset (observed 2026-05-07). 60s tick
    // re-fetches and atomically replaces the in-memory cache; counters
    // surface in /api/exposure/diagnostics.mappings. Singleton + hosted so
    // the controller resolves the same instance for the diagnostics block.
    builder.Services.AddSingleton<MappingRefreshService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<MappingRefreshService>());
    // -----------------------------------------------------------------------

    builder.Services.AddControllers()
        .AddJsonOptions(o =>
        {
            // Serialize enums as strings everywhere (BridgeSide, BridgeSource, ...)
            // so the React side can type them as unions ("BUY" | "SELL") instead of ints.
            o.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter());
        });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // CORS for React dev server
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    var app = builder.Build();

    // Wire alert persistence into broadcast service
    {
        var broadcast = app.Services.GetRequiredService<ExposureBroadcastService>();
        var supabaseForAlerts = app.Services.GetRequiredService<IDataStore>();
        broadcast.SetAlertPersistCallback(async alerts =>
            await supabaseForAlerts.InsertAlertEventsAsync(alerts));
    }

    // Load symbol mappings from Supabase on startup. Routed through
    // MappingRefreshService so the same retry path + counter state is
    // shared between startup and the 60s background refresh tick.
    // GetMappingsAsync now retries 3x on transient TLS resets (Phase 2.21);
    // if all retries fail, the cache stays empty here and the background
    // service auto-heals within 60s.
    using (var scope = app.Services.CreateScope())
    {
        var supabase = scope.ServiceProvider.GetRequiredService<IDataStore>();
        var mappingRefresh = app.Services.GetRequiredService<MappingRefreshService>();
        var ok = await mappingRefresh.RefreshOnceAsync();
        Log.Information(
            "Startup mapping load: ok={Ok}, count={Count}, consecutiveFailures={Failures}",
            ok, mappingRefresh.LastFetchCount, mappingRefresh.ConsecutiveFailures);

        var alertRules = await supabase.GetAlertRulesAsync();
        alertEngine.LoadThresholds(alertRules);
        Log.Information("Loaded {Count} alert rules from Supabase", alertRules.Count);

        if (alertRules.Count > 0)
        {
            var broadcast = app.Services.GetRequiredService<ExposureBroadcastService>();
            broadcast.MarkDirty();
        }

        // Bridge feed bootstrap — pick mode from Supabase bridge_settings if present, else appsettings.
        // When bridge_settings.enabled = false, the entire feed stays dormant (no Live poll,
        // no Stub synthesis). UI + code are untouched so it can be turned back on from Settings.
        var bridgeHost = app.Services.GetRequiredService<BridgeFeedHost>();
        var bridgeSettings = await supabase.GetBridgeSettingsAsync();
        // Centroid:Enabled = false (env Centroid__Enabled=false) keeps the feed dormant whatever
        // bridge_settings says: a second instance (a feed test) must not open its own Centroid session.
        var centroidAllowed = app.Configuration.GetValue("Centroid:Enabled", true);
        if (!centroidAllowed)
        {
            Log.Information("Centroid Bridge feed is DISABLED by config (Centroid:Enabled = false) — skipping startup");
        }
        else if (bridgeSettings?.Enabled == false)
        {
            Log.Information("Centroid Bridge feed is DISABLED in bridge_settings — skipping startup");
        }
        else
        {
            // v2: the ONLY feed is Live (real Centroid dropcopy). It starts solely when the
            // settings are enabled AND fully configured; otherwise the host stays dormant.
            // There is no synthetic fallback any more -- a failure leaves it dormant rather
            // than quietly producing fabricated pairs, which is what v1 did for five months.
            var ready = bridgeSettings?.Enabled == true && bridgeSettings.IsLoginReady();
            if (!ready)
            {
                Log.Information("Centroid Bridge feed dormant (no Live credentials configured)");
                await bridgeHost.SwitchAsync(BridgeFeedHost.DormantMode);
            }
            else
            {
                try
                {
                    await bridgeHost.SwitchAsync("Live");
                    Log.Information("Centroid Bridge feed started in Live mode");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to start the Live Bridge feed; leaving it dormant");
                    try { await bridgeHost.SwitchAsync(BridgeFeedHost.DormantMode); } catch { /* ignore */ }
                }
            }
        }
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors();

    // Serve the React SPA from wwwroot. UseDefaultFiles rewrites / -> /index.html;
    // UseStaticFiles serves the rest (assets/, favicon, etc.).
    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.UseWebSockets();

    // permessage-deflate compression (Phase 2.18) — shrinks WS frames ~60-80%
    // for our payload (lots of repeated symbol names, JSON keys, similar
    // floating-point shapes). Critical for dealers accessing the dashboard
    // over WAN where uplink bandwidth from the live VPS is the bottleneck.
    // `DangerousEnableCompression` is ASP.NET Core's name — the "Dangerous"
    // prefix flags CRIME-style attacks when mixing compression with secrets
    // in the same channel. We send only public market data (no auth tokens
    // or session cookies in the WS payload), so the risk doesn't apply here.
    var wsAcceptCompressed = new WebSocketAcceptContext
    {
        DangerousEnableCompression = true,
        // Keep the deflate context between frames — better compression ratio
        // for our high-frequency tick stream (each frame is similar to the
        // previous one, so the dictionary stays warm).
        DisableServerContextTakeover = false
    };

    // WebSocket endpoint for real-time exposure updates
    app.Map("/ws/exposure", async (HttpContext context) =>
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        var ws = await context.WebSockets.AcceptWebSocketAsync(wsAcceptCompressed);
        var broadcastService = context.RequestServices.GetRequiredService<ExposureBroadcastService>();
        var clientId = Guid.NewGuid().ToString();

        broadcastService.AddClient(clientId, ws);

        // Keep connection alive — read until client disconnects
        var buffer = new byte[1024];
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
            }
        }
        catch (WebSocketException) { }
        finally
        {
            broadcastService.RemoveClient(clientId);
            if (ws.State == WebSocketState.Open)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        }
    });

    // WebSocket endpoint for Bridge Execution Analysis (Phase 2.5)
    app.Map("/ws/bridge", async (HttpContext context) =>
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        var ws = await context.WebSockets.AcceptWebSocketAsync(wsAcceptCompressed);
        var bridgeBroadcast = context.RequestServices.GetRequiredService<BridgeBroadcastService>();
        var clientId = Guid.NewGuid().ToString();
        bridgeBroadcast.AddClient(clientId, ws);

        var buffer = new byte[1024];
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;
            }
        }
        catch (WebSocketException) { }
        finally
        {
            bridgeBroadcast.RemoveClient(clientId);
            if (ws.State == WebSocketState.Open)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        }
    });

    app.MapControllers();

    // SPA fallback: any unmatched non-API route serves index.html so React Router
    // can handle it client-side (deep links to /exposure, /compare, etc.).
    app.MapFallbackToFile("index.html");

    Log.Information("Coverage Manager API starting on http://localhost:5000");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Mirrors the canonical-key normalization the `aggregate_bbook_settled_pnl`
// SQL function uses (UPPER + strip trailing '.xxx' lowercase suffix or trailing
// dashes). Used by the deal_settled WS broadcast so the live overlay lands on
// the same row the next REST refresh will see.
static string NormalizeCanonicalKey(string? raw)
{
    if (string.IsNullOrEmpty(raw)) return string.Empty;
    var s = raw.Trim();
    var dot = s.LastIndexOf('.');
    if (dot >= 0 && s.Length - dot >= 2 && s.Length - dot <= 4)
    {
        var suffix = s[(dot + 1)..];
        bool allLetters = suffix.Length > 0;
        for (int i = 0; i < suffix.Length; i++)
            if (!char.IsLetter(suffix[i])) { allLetters = false; break; }
        if (allLetters) s = s[..dot];
    }
    while (s.EndsWith('-')) s = s[..^1];
    return s.ToUpperInvariant();
}
