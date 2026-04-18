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
    var exposureEngine = new ExposureEngine(positionManager);
    var dealStore = new DealStore();

    var alertEngine = new AlertEngine(exposureEngine, positionManager);

    builder.Services.AddSingleton(positionManager);
    builder.Services.AddSingleton(priceCache);
    builder.Services.AddSingleton(exposureEngine);
    builder.Services.AddSingleton(dealStore);
    builder.Services.AddSingleton(alertEngine);

    // Supabase HTTP client
    builder.Services.AddHttpClient<SupabaseService>();
    builder.Services.AddSingleton<SupabaseService>(sp =>
        new SupabaseService(
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SupabaseService)),
            sp.GetRequiredService<ILogger<SupabaseService>>()));

    // Broadcast service (WebSocket push)
    builder.Services.AddSingleton<ExposureBroadcastService>();

    // MT5 Manager connection (reads accounts from Supabase, connects, snapshots positions)
    builder.Services.AddSingleton<MT5ManagerConnection>(sp =>
    {
        var supabase = sp.GetRequiredService<SupabaseService>();
        var broadcast = sp.GetRequiredService<ExposureBroadcastService>();
        return new MT5ManagerConnection(
            sp.GetRequiredService<ILogger<MT5ManagerConnection>>(),
            positionManager,
            priceCache,
            dealStore,
            async () => await supabase.GetAccountSettingsAsync(),
            () => broadcast.MarkDirty(),
            async accounts => await supabase.UpsertTradingAccountsAsync(accounts),
            async source => await supabase.GetLastDealTimeAsync(source));
    });
    builder.Services.AddHostedService(sp => sp.GetRequiredService<MT5ManagerConnection>());

    // MT5 Coverage connection (LP account — reads coverage positions)
    builder.Services.AddSingleton<MT5CoverageConnection>(sp =>
    {
        var supabase = sp.GetRequiredService<SupabaseService>();
        var broadcast = sp.GetRequiredService<ExposureBroadcastService>();
        return new MT5CoverageConnection(
            sp.GetRequiredService<ILogger<MT5CoverageConnection>>(),
            positionManager,
            priceCache,
            async () => await supabase.GetAccountSettingsAsync(),
            () => broadcast.MarkDirty());
    });
    builder.Services.AddHostedService(sp => sp.GetRequiredService<MT5CoverageConnection>());

    // Data sync service (persists deals to Supabase, detects modifications)
    builder.Services.AddHostedService<DataSyncService>(sp =>
        new DataSyncService(
            sp.GetRequiredService<SupabaseService>(),
            dealStore,
            sp.GetRequiredService<ILogger<DataSyncService>>()));

    // ---- Phase 2.5: Bridge Execution Analysis (Centroid Dropcopy feed) ----
    // Pairing window and feed mode are read from config; defaults are safe (Stub + 10s).
    var bridgePairingWindowMs = builder.Configuration.GetValue("Centroid:PairingWindowMs", 10_000);

    builder.Services.AddSingleton<BridgeExecutionStore>(sp =>
        new BridgeExecutionStore(
            bridgePairingWindowMs,
            sp.GetRequiredService<ILogger<BridgeExecutionStore>>()));

    builder.Services.AddSingleton<BridgeBroadcastService>();

    builder.Services.AddHttpClient<BridgeSupabaseWriter>();
    builder.Services.AddSingleton<BridgeSupabaseWriter>(sp =>
        new BridgeSupabaseWriter(
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(BridgeSupabaseWriter)),
            sp.GetRequiredService<ILogger<BridgeSupabaseWriter>>()));

    // Both feed implementations are registered as singletons; BridgeFeedHost picks one at runtime.
    builder.Services.AddSingleton<StubCentroidBridgeService>();
    builder.Services.AddHttpClient<RestCentroidBridgeService>();
    builder.Services.AddSingleton<RestCentroidBridgeService>();
    builder.Services.AddSingleton<BridgeFeedHost>();
    // Controllers & worker depend on ICentroidBridgeService — route that to the host facade.
    builder.Services.AddSingleton<ICentroidBridgeService, BridgeFeedHostAdapter>();

    builder.Services.AddHostedService<BridgeExecutionWorker>();

    // Coverage-side deal index — polls the Python collector's /deals/raw and maps
    // MT5 order_ticket -> MT5 deal_ticket on the 96900 account. Enables deal-per-deal
    // reconciliation on the COV OUT side (Centroid maker_order_id echoes onto 96900 as order_ticket).
    builder.Services.AddHttpClient<CoverageDealIndex>();
    builder.Services.AddSingleton<CoverageDealIndex>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<CoverageDealIndex>());

    // Period P&L scheduler — dispatches snapshot_schedules every 60s.
    builder.Services.AddSingleton<ExposureSnapshotService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<ExposureSnapshotService>());

    // Nightly deal reconciliation — finds ghosts (Supa has, MT5 doesn't) and patches
    // modifications so our historical P&L stays aligned with MT5 Manager over time.
    builder.Services.AddSingleton<ReconciliationService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<ReconciliationService>());
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
        var supabaseForAlerts = app.Services.GetRequiredService<SupabaseService>();
        broadcast.SetAlertPersistCallback(async alerts =>
            await supabaseForAlerts.InsertAlertEventsAsync(alerts));
    }

    // Load symbol mappings from Supabase on startup
    using (var scope = app.Services.CreateScope())
    {
        var supabase = scope.ServiceProvider.GetRequiredService<SupabaseService>();
        var mappings = await supabase.GetMappingsAsync();
        positionManager.LoadMappings(mappings);
        Log.Information("Loaded {Count} symbol mappings from Supabase", mappings.Count);

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
        if (bridgeSettings?.Enabled == false)
        {
            Log.Information("Centroid Bridge feed is DISABLED in bridge_settings — skipping startup");
        }
        else
        {
            var initialMode = bridgeSettings?.Enabled == true && bridgeSettings.IsLoginReady()
                ? "Live"
                : (app.Configuration["Centroid:Mode"] ?? "Stub");
            try
            {
                await bridgeHost.SwitchAsync(initialMode);
                Log.Information("Centroid Bridge feed started in {Mode} mode", initialMode);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start Bridge feed in {Mode}, falling back to Stub", initialMode);
                try { await bridgeHost.SwitchAsync("Stub"); } catch { /* ignore */ }
            }
        }
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors();
    app.UseWebSockets();

    // WebSocket endpoint for real-time exposure updates
    app.Map("/ws/exposure", async (HttpContext context) =>
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        var ws = await context.WebSockets.AcceptWebSocketAsync();
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

        var ws = await context.WebSockets.AcceptWebSocketAsync();
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
