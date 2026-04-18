using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoverageManager.Core.Models.Bridge;

namespace CoverageManager.Api.Services;

/// <summary>
/// Centroid CS 360 REST + WebSocket integration (spec v1.7).
/// See /docs/centroid/rest-and-websocket.md for the authoritative field reference.
///
/// Lifecycle:
///   1. POST /v2/api/login with username/password → caches the Bearer token.
///   2. Refreshes via GET /v1/api/refresh_token before expiry.
///   3. Opens wss://.../ws?token=...&q={client_code}, subscribes to live_trades.
///   4. Each incoming live_trades event is normalized into a BridgeDeal and fanned
///      out to all subscribers (pairing engine + worker).
/// </summary>
public class RestCentroidBridgeService : BackgroundService, ICentroidBridgeService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly SupabaseService _supabase;
    private readonly ILogger<RestCentroidBridgeService> _logger;

    private readonly ConcurrentBag<BridgeDeal> _buffer = new();
    private readonly List<Action<BridgeDeal>> _subscribers = new();
    private readonly object _subLock = new();
    // Per-CenOrdId client markup detail from /v1/api/orders_report (CLIENT side).
    private readonly ConcurrentDictionary<string, ClientOrderDetail> _clientDetails = new();

    private BridgeSettings? _settings;
    private string? _token;
    private DateTime _tokenExpiresUtc = DateTime.MinValue;
    private CentroidConnectionState _state = CentroidConnectionState.Disconnected;
    private string? _lastError;
    private long _msgCount;
    private DateTime? _lastMsgUtc;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public RestCentroidBridgeService(
        IHttpClientFactory httpFactory,
        SupabaseService supabase,
        ILogger<RestCentroidBridgeService> logger)
    {
        _httpFactory = httpFactory;
        _supabase = supabase;
        _logger = logger;
    }

    public CentroidHealth GetHealth() => new()
    {
        Mode = "Live",
        State = _state,
        LastError = _lastError,
        LastMessageUtc = _lastMsgUtc,
        MessagesReceived = _msgCount,
    };

    public Task<IReadOnlyList<BridgeDeal>> GetDealsAsync(
        DateTime fromUtc, DateTime toUtc, string? canonicalSymbol = null, CancellationToken ct = default)
    {
        var q = _buffer
            .Where(d => d.TimeUtc >= fromUtc && d.TimeUtc <= toUtc)
            .Where(d => canonicalSymbol == null ||
                        string.Equals(d.CanonicalSymbol ?? d.Symbol, canonicalSymbol, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => d.TimeUtc)
            .ToList();
        return Task.FromResult<IReadOnlyList<BridgeDeal>>(q);
    }

    public IDisposable Subscribe(Action<BridgeDeal> onDeal)
    {
        lock (_subLock) _subscribers.Add(onDeal);
        return new Unsubscribe(() =>
        {
            lock (_subLock) _subscribers.Remove(onDeal);
        });
    }

    public ClientOrderDetail? GetClientDetail(string cenOrdId)
    {
        if (string.IsNullOrEmpty(cenOrdId)) return null;
        return _clientDetails.TryGetValue(cenOrdId, out var d) ? d : null;
    }

    // ---------------------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------------------

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _settings = await _supabase.GetBridgeSettingsAsync();
        if (_settings == null || !_settings.IsLoginReady())
        {
            _state = CentroidConnectionState.Error;
            _lastError = "bridge_settings not configured (missing base_url, client_code, username, or password)";
            _logger.LogWarning("RestCentroidBridgeService: {Error}", _lastError);
            return;
        }

        _logger.LogInformation("RestCentroidBridgeService starting — base {Base}, client {Client}, user {User}",
            _settings.BaseUrl, _settings.ClientCode, _settings.Username);

        // Outer reconnect loop — keeps the WS alive across token refreshes and network blips.
        var attempt = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_token == null || DateTime.UtcNow >= _tokenExpiresUtc.AddMinutes(-2))
                {
                    await LoginAsync(stoppingToken);
                }

                _state = CentroidConnectionState.Connecting;

                // Run WS (CLIENT fills), maker-orders poller (COV OUT), and
                // orders-report poller (CLIENT markup detail) in parallel.
                // If any task ends we tear down and reconnect — keeps them in sync.
                using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var wsTask     = RunWebSocketAsync(loopCts.Token);
                var covTask    = RunMakerOrdersPollerAsync(loopCts.Token);
                var clientTask = RunOrdersReportPollerAsync(loopCts.Token);
                var completed = await Task.WhenAny(wsTask, covTask, clientTask);
                loopCts.Cancel();
                try { await Task.WhenAll(wsTask, covTask, clientTask); } catch { /* surfaced via state */ }
                if (completed.IsFaulted) throw completed.Exception!.GetBaseException();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _state = CentroidConnectionState.Error;
                _logger.LogError(ex, "Bridge WS loop crashed on attempt {Attempt}", attempt + 1);
            }

            if (stoppingToken.IsCancellationRequested) break;

            attempt++;
            var delay = TimeSpan.FromSeconds(Math.Min(30, 2 * attempt));
            _logger.LogInformation("Reconnecting to Centroid in {Delay}s", delay.TotalSeconds);
            try { await Task.Delay(delay, stoppingToken); } catch (OperationCanceledException) { break; }
        }

        _state = CentroidConnectionState.Disconnected;
        _logger.LogInformation("RestCentroidBridgeService stopped");
    }

    // ---------------------------------------------------------------------------
    // Auth
    // ---------------------------------------------------------------------------

    private sealed class LoginResponse
    {
        [JsonPropertyName("token")] public string? Token { get; set; }
        [JsonPropertyName("expire")] public string? Expire { get; set; }
    }

    /// <summary>
    /// Stand-alone login call. Used both by the worker loop and by the
    /// <see cref="TestConnectionAsync"/> helper so the UI can verify creds before flipping Enabled.
    /// </summary>
    // bridge.centroidsol.com serves login at BOTH /v2/login and /v2/api/login but they have
    // different semantics:
    //   /v2/login      -> 404 + JSON {"error":"USER_NOT_FOUND (user)","status":false} on bad user,
    //                     401 on bad password.
    //   /v2/api/login  -> 401 {"message":"Invalid credentials","status":false} for either case.
    // We try /v2/login first because its errors are more informative. Only retry /v2/api/login
    // when /v2/login returns a response that's clearly "wrong path" (non-JSON or missing the
    // expected status field) — NOT on every 404.
    private const string PrimaryLoginPath = "/v2/login";
    private const string FallbackLoginPath = "/v2/api/login";

    public static async Task<(bool ok, string? token, string? error, int status)> LoginOnceAsync(
        HttpClient http, string baseUrl, string username, string password, CancellationToken ct = default)
    {
        // 1. Try the primary path.
        var primary = await AttemptLoginAsync(http, baseUrl, PrimaryLoginPath, username, password, ct);
        if (primary.ok) return (true, primary.token, null, primary.status);

        // If the server at the primary path acknowledged our request (returned a JSON error),
        // that's the authoritative answer — don't second-guess it by hitting a different path.
        if (primary.serverAnswered) return (false, null, primary.error, primary.status);

        // 2. Otherwise the primary path looks dead (HTML 404, network error, etc.) — try fallback.
        var fallback = await AttemptLoginAsync(http, baseUrl, FallbackLoginPath, username, password, ct);
        return fallback.ok
            ? (true, fallback.token, null, fallback.status)
            : (false, null, fallback.error ?? primary.error, fallback.status);
    }

    private static async Task<(bool ok, string? token, string? error, int status, bool serverAnswered)>
        AttemptLoginAsync(HttpClient http, string baseUrl, string path, string username, string password, CancellationToken ct)
    {
        try
        {
            var url = baseUrl.TrimEnd('/') + path;
            var res = await http.PostAsJsonAsync(url, new { username, password }, JsonOpts, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            if (res.IsSuccessStatusCode)
            {
                var bodyToken = TryParseLoginBody(body);
                if (!string.IsNullOrWhiteSpace(bodyToken))
                    return (true, bodyToken, null, (int)res.StatusCode, true);

                var cookieToken = ExtractJwtFromSetCookie(res);
                if (!string.IsNullOrWhiteSpace(cookieToken))
                    return (true, cookieToken, null, (int)res.StatusCode, true);

                return (false, null, "Response missing token field", (int)res.StatusCode, true);
            }

            // Non-2xx — surface the server's own error text if it returned JSON.
            var parsed = TryParseError(body);
            var msg = parsed != null
                ? $"HTTP {(int)res.StatusCode}: {parsed}"
                : $"HTTP {(int)res.StatusCode}: {TrimBody(body)}";
            return (false, null, msg, (int)res.StatusCode, parsed != null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message, 0, false);
        }
    }

    private static string? TryParseError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            // Centroid uses "error" (on /v2/login) or "message" (on /v2/api/login). Either is proof
            // that the server understood the request.
            if (root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                return e.GetString();
            if (root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                return m.GetString();
            return null;
        }
        catch { return null; }
    }

    private static string? TryParseLoginBody(string body)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<LoginResponse>(body, JsonOpts);
            return parsed?.Token;
        }
        catch { return null; }
    }

    private static string? ExtractJwtFromSetCookie(HttpResponseMessage res)
    {
        if (!res.Headers.TryGetValues("Set-Cookie", out var cookies)) return null;
        foreach (var c in cookies)
        {
            var m = System.Text.RegularExpressions.Regex.Match(c, @"jwt=([^;]+)");
            if (m.Success) return m.Groups[1].Value;
        }
        return null;
    }

    private async Task LoginAsync(CancellationToken ct)
    {
        if (_settings == null) throw new InvalidOperationException("settings not loaded");
        using var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(15);
        var (ok, token, error, _) = await LoginOnceAsync(http, _settings.BaseUrl, _settings.Username, _settings.Password, ct);
        if (!ok)
        {
            _lastError = error;
            _state = CentroidConnectionState.Error;
            throw new InvalidOperationException("Centroid login failed: " + error);
        }
        _token = token;
        // Centroid `expire` is typically a string duration ("168h") or an ISO date. Conservative
        // default: assume 1 hour; the refresh path handles the real expiry either way.
        _tokenExpiresUtc = DateTime.UtcNow.AddHours(1);
        _logger.LogInformation("Centroid REST login succeeded");
    }

    // ---------------------------------------------------------------------------
    // WebSocket — live_trades channel
    // ---------------------------------------------------------------------------

    private async Task RunWebSocketAsync(CancellationToken ct)
    {
        if (_settings == null || _token == null) return;

        var wsUri = BuildWebSocketUri(_settings.BaseUrl, _token, _settings.ClientCode);
        using var ws = new ClientWebSocket();

        // CS 360 docs don't require subprotocols; keep default timeouts.
        await ws.ConnectAsync(wsUri, ct);
        _state = CentroidConnectionState.LoggedIn;
        _lastError = null;
        _logger.LogInformation("Centroid WS connected");

        // Subscribe to live_trades.
        var sub = JsonSerializer.SerializeToUtf8Bytes(new { key = "live_trades" }, JsonOpts);
        await ws.SendAsync(sub, WebSocketMessageType.Text, true, ct);

        var buffer = new byte[64 * 1024];
        var acc = new StringBuilder();
        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            acc.Clear();
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                    return;
                }
                acc.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            } while (!result.EndOfMessage);

            try { HandleWsMessage(acc.ToString()); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to parse WS message"); }
        }
    }

    // ---------------------------------------------------------------------------
    // Maker Orders poller — fetches COV_OUT legs via POST /v1/api/maker_orders.
    // live_trades WebSocket only emits CLIENT (bridge-side) fills; the outbound LP
    // legs only appear in this REST report, so we poll it on a short interval.
    // ---------------------------------------------------------------------------

    private const int MakerPollIntervalMs = 10_000;
    // IMPORTANT: Centroid's /v1/api/maker_orders endpoint has an undocumented minimum
    // lookback — windows shorter than ~11 hours return 0 rows regardless of activity.
    // Verified empirically 2026-04-16: 30m/1h/3h/6h/8h = 0 rows, 11h = 943, 12h = 1827.
    // 12h is the minimum that's reliable; going beyond wastes memory re-parsing old rows.
    private static readonly TimeSpan MakerLookback = TimeSpan.FromHours(12);

    /// <summary>
    /// Columns we need on every maker_orders poll. Centroid returns empty object rows when
    /// this list is empty — we MUST supply it.
    /// </summary>
    private static readonly string[] MakerOrdersColumns =
    {
        "cen_ord_id", "cen_client_ord_id", "symbol", "party_symbol",
        "side_value",
        "volume_value", "fill_volume_value", "tot_fill_volume_value",
        "avg_price", "price", "raw_avg_price",
        "recv_time_value",
        "ext_login", "ext_group", "ext_order", "ext_posid", "ext_dealid", "ext_markup",
        "state", "res_state_value",
        "maker_order_id_value", "lpsid",
        "node", "node_account",
    };

    private sealed class MakerOrdersRequest
    {
        // NOTE: Centroid's docs say start_date/end_date are integers but do NOT specify the
        // unit. Empirically (verified 2026-04-16 via curl), the endpoint expects UNIX MICROSECONDS.
        // Seconds and milliseconds return an empty array.
        [JsonPropertyName("start_date")] public long StartDate { get; set; }
        [JsonPropertyName("end_date")]   public long EndDate   { get; set; }
        [JsonPropertyName("symbol")]     public string[] Symbol     { get; set; } = Array.Empty<string>();
        [JsonPropertyName("account")]    public string[] Account    { get; set; } = Array.Empty<string>();
        [JsonPropertyName("login")]      public string[] Login      { get; set; } = Array.Empty<string>();
        [JsonPropertyName("group")]      public string[] Group      { get; set; } = Array.Empty<string>();
        [JsonPropertyName("cen_ord_id")] public string[] CenOrdId   { get; set; } = Array.Empty<string>();
        [JsonPropertyName("order")]      public string[] Order      { get; set; } = Array.Empty<string>();
        [JsonPropertyName("execution")]  public string[] Execution  { get; set; } = Array.Empty<string>();
        [JsonPropertyName("risk_account")] public string[] RiskAccount    { get; set; } = Array.Empty<string>();
        [JsonPropertyName("markup_models")] public string[] MarkupModels  { get; set; } = Array.Empty<string>();
        [JsonPropertyName("displayed_columns")] public string[] DisplayedColumns { get; set; } = MakerOrdersColumns;
    }

    private sealed class MakerOrderRow
    {
        [JsonPropertyName("cen_ord_id")]              public string? CenOrdId { get; set; }
        [JsonPropertyName("cen_client_ord_id")]       public string? CenClientOrdId { get; set; }
        [JsonPropertyName("client_ord_id")]           public string? ClientOrdId { get; set; }
        [JsonPropertyName("ext_order")]               public long?   ExtOrder { get; set; }
        [JsonPropertyName("ext_dealid")]              public long?   ExtDealId { get; set; }
        [JsonPropertyName("ext_login")]               public long?   ExtLogin { get; set; }
        [JsonPropertyName("ext_group")]               public string? ExtGroup { get; set; }
        [JsonPropertyName("ext_posid")]               public long?   ExtPosId { get; set; }
        [JsonPropertyName("ext_markup")]              public decimal? ExtMarkup { get; set; }
        [JsonPropertyName("symbol")]                  public string? Symbol { get; set; }
        [JsonPropertyName("party_symbol")]            public string? PartySymbol { get; set; }
        [JsonPropertyName("side_value")]              public string? SideValue { get; set; }
        [JsonPropertyName("fill_volume_value")]       public decimal? FillVolume { get; set; }
        [JsonPropertyName("tot_fill_volume_value")]   public decimal? TotFillVolume { get; set; }
        [JsonPropertyName("volume_value")]            public decimal? Volume { get; set; }
        [JsonPropertyName("avg_price")]               public decimal? AvgPrice { get; set; }
        [JsonPropertyName("price")]                   public decimal? Price { get; set; }
        [JsonPropertyName("raw_avg_price")]           public decimal? RawAvgPrice { get; set; }
        [JsonPropertyName("recv_time_value")]         public string? RecvTime { get; set; }
        [JsonPropertyName("state")]                   public string? State { get; set; }
        [JsonPropertyName("res_state_value")]         public string? ResStateValue { get; set; }
        [JsonPropertyName("maker_order_id_value")]    public string? MakerOrderId { get; set; }
        [JsonPropertyName("lpsid")]                   public string? Lpsid { get; set; }
        [JsonPropertyName("node")]                    public string? Node { get; set; }
        [JsonPropertyName("node_account")]            public string? NodeAccount { get; set; }
    }

    private async Task RunMakerOrdersPollerAsync(CancellationToken ct)
    {
        if (_settings == null || _token == null) return;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Always poll the full MakerLookback window. Centroid requires >= 11h,
                // our store dedupes by DealId so replays are free.
                var nowUtc = DateTime.UtcNow;
                await PollOnceAsync(nowUtc - MakerLookback, nowUtc.AddSeconds(5), ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Maker-orders poll failed (will retry)");
                _lastError = ex.Message;
            }

            try { await Task.Delay(MakerPollIntervalMs, ct); }
            catch (OperationCanceledException) { throw; }
        }
    }

    private async Task PollOnceAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        if (_settings == null || _token == null) return;

        using var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(15);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("coverage-manager/1.0");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        http.DefaultRequestHeaders.Add("x-forward-client", _settings.ClientCode);
        http.DefaultRequestHeaders.Add("x-forward-user",   _settings.Username);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var body = new MakerOrdersRequest
        {
            // Centroid expects MICROSECONDS.
            StartDate = new DateTimeOffset(fromUtc, TimeSpan.Zero).ToUnixTimeMilliseconds() * 1000L,
            EndDate   = new DateTimeOffset(toUtc,   TimeSpan.Zero).ToUnixTimeMilliseconds() * 1000L,
            DisplayedColumns = MakerOrdersColumns,
        };

        var url = _settings.BaseUrl.TrimEnd('/') + "/v1/api/maker_orders";
        using var res = await http.PostAsJsonAsync(url, body, JsonOpts, ct);

        if ((int)res.StatusCode == 401)
        {
            _logger.LogInformation("maker_orders: 401 — forcing re-login");
            _token = null;
            throw new InvalidOperationException("token rejected");
        }
        if (!res.IsSuccessStatusCode)
        {
            // Only buffer body for error logging (small typical case).
            var errText = await res.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("maker_orders HTTP {Status}: {Body}", (int)res.StatusCode, TrimBody(errText));
            return;
        }

        // Stream-based deserialization avoids buffering the whole 1-5 MB response into a
        // string (otherwise lands on the LOH every 10s and bloats working set over time).
        List<MakerOrderRow>? rows;
        try
        {
            using var stream = await res.Content.ReadAsStreamAsync(ct);
            rows = await JsonSerializer.DeserializeAsync<List<MakerOrderRow>>(stream, JsonOpts, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "maker_orders: unexpected response shape");
            return;
        }
        if (rows == null || rows.Count == 0) return;

        var published = 0;
        var skipped = 0;
        foreach (var r in rows)
        {
            var deal = NormalizeMakerOrder(r);
            if (deal != null) { Publish(deal); published++; }
            else skipped++;
        }
        _logger.LogInformation(
            "maker_orders poll: {Total} rows, published {Pub} COV_OUT, skipped {Skip} (window {From:HH:mm:ss}..{To:HH:mm:ss})",
            rows.Count, published, skipped, fromUtc, toUtc);
    }

    private static BridgeDeal? NormalizeMakerOrder(MakerOrderRow r)
    {
        if (string.IsNullOrEmpty(r.CenOrdId) || string.IsNullOrEmpty(r.Symbol)) return null;

        // Every client order has multiple legs: one per LP plus (usually) a "B_BOOK" leg that
        // represents the internally-absorbed portion. Only EXTERNAL legs are actual coverage.
        // See docs/centroid/rest-and-websocket.md: Leg with lpsid == "B_BOOK" = internal hedge.
        if (string.Equals(r.Lpsid, "B_BOOK", StringComparison.OrdinalIgnoreCase)) return null;

        // Per-leg fill is tot_fill_volume_value. fill_volume_value is the parent order's total.
        var legVol = r.TotFillVolume ?? r.FillVolume ?? r.Volume ?? 0m;
        if (legVol <= 0m) return null;

        var price = r.AvgPrice ?? r.Price ?? r.RawAvgPrice ?? 0m;
        if (price <= 0m) return null;

        var side = string.Equals(r.SideValue, "BUY", StringComparison.OrdinalIgnoreCase)
            ? BridgeSide.BUY
            : BridgeSide.SELL;

        // Stable per-leg ID so re-polling doesn't create duplicate CovFills.
        var dealId = !string.IsNullOrEmpty(r.CenClientOrdId)
            ? $"mk|{r.CenOrdId}|{r.CenClientOrdId}"
            : $"mk|{r.CenOrdId}|{r.MakerOrderId ?? r.ExtDealId?.ToString() ?? r.ExtOrder?.ToString() ?? Guid.NewGuid().ToString("N")}";

        return new BridgeDeal
        {
            DealId = dealId,
            CenOrdId = r.CenOrdId!,
            Symbol = r.Symbol!,
            Source = BridgeSource.COV_OUT,
            Side = side,
            Volume = legVol,
            Price = price,
            TimeUtc = ParseUtc(r.RecvTime) ?? DateTime.UtcNow,
            LpName = r.Lpsid ?? r.MakerOrderId,
            MtLogin = r.ExtLogin is long l and > 0 ? (ulong)l : null,
            MtGroup = r.ExtGroup,
            MtTicket = r.ExtOrder is long o and > 0 ? (ulong)o : null,
            MtDealId = r.ExtDealId is long d and > 0 ? (ulong)d : null,
            PositionId = r.ExtPosId is long p and > 0 ? (ulong)p : null,
            ExternalMarkup = r.ExtMarkup,
            MakerOrderId = r.MakerOrderId,
            RawPrice = r.RawAvgPrice,
        };
    }

    // ---------------------------------------------------------------------------
    // Orders Report poller — fetches CLIENT-side markup + book-split via
    // POST /v1/api/orders_report. live_trades WS gives the fills but not
    // total_markup / ext_markup / a_avg_price / b_avg_price / req_avg_price —
    // those come from orders_report and enrich ExecutionPair at read time.
    // ---------------------------------------------------------------------------

    private const int OrdersReportPollIntervalMs = 10_000;
    // orders_report has no documented minimum lookback (unlike maker_orders).
    // We only need recent NEW orders — anything older is already cached in _clientDetails.
    // 30min gives generous catch-up room for network blips / backfills.
    private static readonly TimeSpan OrdersReportLookback = TimeSpan.FromMinutes(30);
    // Evict client-detail entries older than this to keep the dict bounded.
    private static readonly TimeSpan ClientDetailsRetention = TimeSpan.FromHours(6);

    private static readonly string[] OrdersReportColumns =
    {
        "cen_ord_id", "client_ord_id", "symbol", "party_symbol",
        "side",
        "volume", "fill_volume", "afill_volume", "bfill_volume",
        "volume_abook", "volume_bbook",
        "avg_price", "price", "req_avg_price",
        "a_avg_price", "b_avg_price",
        "a_tot_fill_volume", "b_tot_fill_volume",
        "total_markup", "ext_markup",
        "ext_login", "ext_group_raw", "ext_order", "ext_posid", "ext_dealid",
        "recv_time_msc",
        "maker", "maker_cat", "node", "node_account",
        "res_state",
    };

    private sealed class OrdersReportRequest
    {
        [JsonPropertyName("start_date")] public long StartDate { get; set; }
        [JsonPropertyName("end_date")]   public long EndDate   { get; set; }
        [JsonPropertyName("symbol")]     public string[] Symbol     { get; set; } = Array.Empty<string>();
        [JsonPropertyName("account")]    public string[] Account    { get; set; } = Array.Empty<string>();
        [JsonPropertyName("login")]      public string[] Login      { get; set; } = Array.Empty<string>();
        [JsonPropertyName("group")]      public string[] Group      { get; set; } = Array.Empty<string>();
        [JsonPropertyName("cen_ord_id")] public string[] CenOrdId   { get; set; } = Array.Empty<string>();
        [JsonPropertyName("order")]      public string[] Order      { get; set; } = Array.Empty<string>();
        [JsonPropertyName("execution")]  public string[] Execution  { get; set; } = Array.Empty<string>();
        [JsonPropertyName("risk_account")] public string[] RiskAccount    { get; set; } = Array.Empty<string>();
        [JsonPropertyName("markup_models")] public string[] MarkupModels  { get; set; } = Array.Empty<string>();
        [JsonPropertyName("displayed_columns")] public string[] DisplayedColumns { get; set; } = OrdersReportColumns;
    }

    private sealed class OrdersReportRow
    {
        [JsonPropertyName("cen_ord_id")]         public string?  CenOrdId { get; set; }
        [JsonPropertyName("client_ord_id")]      public string?  ClientOrdId { get; set; }
        [JsonPropertyName("symbol")]             public string?  Symbol { get; set; }
        [JsonPropertyName("side")]               public int?     Side { get; set; }    // 1=BUY, 2=SELL on orders_report
        [JsonPropertyName("volume")]             public decimal? Volume { get; set; }
        [JsonPropertyName("fill_volume")]        public decimal? FillVolume { get; set; }
        [JsonPropertyName("afill_volume")]       public decimal? AFillVolume { get; set; }
        [JsonPropertyName("bfill_volume")]       public decimal? BFillVolume { get; set; }
        [JsonPropertyName("avg_price")]          public decimal? AvgPrice { get; set; }
        [JsonPropertyName("price")]              public decimal? Price { get; set; }
        [JsonPropertyName("req_avg_price")]      public decimal? ReqAvgPrice { get; set; }
        [JsonPropertyName("a_avg_price")]        public decimal? AAvgPrice { get; set; }
        [JsonPropertyName("b_avg_price")]        public decimal? BAvgPrice { get; set; }
        [JsonPropertyName("a_tot_fill_volume")]  public decimal? ATotFillVolume { get; set; }
        [JsonPropertyName("b_tot_fill_volume")]  public decimal? BTotFillVolume { get; set; }
        [JsonPropertyName("total_markup")]       public decimal? TotalMarkup { get; set; }
        [JsonPropertyName("ext_markup")]         public decimal? ExtMarkup { get; set; }
        [JsonPropertyName("recv_time_msc")]      public long?    RecvTimeMsc { get; set; }
    }

    private async Task RunOrdersReportPollerAsync(CancellationToken ct)
    {
        if (_settings == null || _token == null) return;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var nowUtc = DateTime.UtcNow;
                await OrdersReportPollOnceAsync(nowUtc - OrdersReportLookback, nowUtc.AddSeconds(5), ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "orders_report poll failed (will retry)");
                _lastError = ex.Message;
            }

            try { await Task.Delay(OrdersReportPollIntervalMs, ct); }
            catch (OperationCanceledException) { throw; }
        }
    }

    private async Task OrdersReportPollOnceAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        if (_settings == null || _token == null) return;

        using var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(15);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("coverage-manager/1.0");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        http.DefaultRequestHeaders.Add("x-forward-client", _settings.ClientCode);
        http.DefaultRequestHeaders.Add("x-forward-user",   _settings.Username);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var body = new OrdersReportRequest
        {
            StartDate = new DateTimeOffset(fromUtc, TimeSpan.Zero).ToUnixTimeMilliseconds() * 1000L,
            EndDate   = new DateTimeOffset(toUtc,   TimeSpan.Zero).ToUnixTimeMilliseconds() * 1000L,
            DisplayedColumns = OrdersReportColumns,
        };

        var url = _settings.BaseUrl.TrimEnd('/') + "/v1/api/orders_report";

        List<OrdersReportRow>? rows = null;
        try
        {
            using var res = await http.PostAsJsonAsync(url, body, JsonOpts, ct);

            if ((int)res.StatusCode == 401)
            {
                _logger.LogInformation("orders_report: 401 — forcing re-login");
                _token = null;
                throw new InvalidOperationException("token rejected");
            }
            if (!res.IsSuccessStatusCode)
            {
                var errText = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("orders_report HTTP {Status}: {Body}", (int)res.StatusCode, TrimBody(errText));
                return;
            }

            // Stream-based deserialization — avoids buffering ~1-3 MB of JSON per poll
            // into a string (goes to LOH otherwise).
            try
            {
                using var stream = await res.Content.ReadAsStreamAsync(ct);
                rows = await JsonSerializer.DeserializeAsync<List<OrdersReportRow>>(stream, JsonOpts, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "orders_report: unexpected response shape");
                return;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "orders_report: HTTP call failed");
            return;
        }

        if (rows == null || rows.Count == 0) return;

        var updated = 0;
        foreach (var r in rows)
        {
            if (string.IsNullOrEmpty(r.CenOrdId)) continue;

            var detail = new ClientOrderDetail
            {
                CenOrdId    = r.CenOrdId,
                ReqAvgPrice = r.ReqAvgPrice,
                AvgPrice    = r.AvgPrice ?? r.Price,
                TotalMarkup = r.TotalMarkup,
                ExtMarkup   = r.ExtMarkup,
                AFillVolume = r.ATotFillVolume ?? r.AFillVolume,
                BFillVolume = r.BTotFillVolume ?? r.BFillVolume,
                AAvgPrice   = r.AAvgPrice,
                BAvgPrice   = r.BAvgPrice,
                RecvTimeUtc = r.RecvTimeMsc.HasValue
                    ? DateTimeOffset.FromUnixTimeMilliseconds(r.RecvTimeMsc.Value / 1000).UtcDateTime
                    : null,
            };
            _clientDetails[r.CenOrdId] = detail;
            updated++;
        }

        // Evict old entries to keep the dict bounded (grows by ~100-500/min otherwise).
        // Entries without a RecvTimeUtc can't be aged out individually, so hard-cap at
        // 100K and drop arbitrary entries if we ever exceed it (shouldn't happen in normal
        // operation since the 30-min lookback + 6h retention covers typical flow).
        var cutoff = DateTime.UtcNow - ClientDetailsRetention;
        var evicted = 0;
        foreach (var kvp in _clientDetails)
        {
            if (kvp.Value.RecvTimeUtc.HasValue && kvp.Value.RecvTimeUtc.Value < cutoff)
            {
                if (_clientDetails.TryRemove(kvp.Key, out _)) evicted++;
            }
        }
        if (_clientDetails.Count > 100_000)
        {
            var drop = _clientDetails.Keys.Take(_clientDetails.Count - 75_000).ToList();
            foreach (var k in drop) _clientDetails.TryRemove(k, out _);
            evicted += drop.Count;
        }

        _logger.LogInformation(
            "orders_report poll: {Total} rows, {Updated} updated, {Evicted} evicted (index size {Index}, window {From:HH:mm:ss}..{To:HH:mm:ss})",
            rows.Count, updated, evicted, _clientDetails.Count, fromUtc, toUtc);
    }

    private static Uri BuildWebSocketUri(string baseUrl, string token, string clientCode)
    {
        var u = new Uri(baseUrl);
        var scheme = u.Scheme == "https" ? "wss" : "ws";
        var builder = new UriBuilder
        {
            Scheme = scheme,
            Host = u.Host,
            Port = u.IsDefaultPort ? (scheme == "wss" ? 443 : 80) : u.Port,
            Path = "/ws",
            Query = $"token={Uri.EscapeDataString(token)}&q={Uri.EscapeDataString(clientCode)}"
        };
        return builder.Uri;
    }

    private sealed class LiveTradesEnvelope
    {
        [JsonPropertyName("live_trades")] public List<LiveTrade>? LiveTrades { get; set; }
    }

    private sealed class LiveTrade
    {
        [JsonPropertyName("cen_ord_id")]        public string? CenOrdId { get; set; }
        [JsonPropertyName("ext_login")]         public long? ExtLogin { get; set; }
        [JsonPropertyName("ext_group")]         public string? ExtGroup { get; set; }
        [JsonPropertyName("ext_order")]         public long? ExtOrder { get; set; }
        [JsonPropertyName("recv_time_value")]   public string? RecvTime { get; set; }
        [JsonPropertyName("node")]              public string? Node { get; set; }
        [JsonPropertyName("node_account")]      public string? NodeAccount { get; set; }
        [JsonPropertyName("symbol")]            public string? Symbol { get; set; }
        [JsonPropertyName("party_symbol")]      public string? PartySymbol { get; set; }
        [JsonPropertyName("side_value")]        public string? SideValue { get; set; }
        [JsonPropertyName("ord_type_value")]    public string? OrdType { get; set; }
        [JsonPropertyName("volume_value")]      public decimal? Volume { get; set; }
        [JsonPropertyName("fill_volume_value")] public decimal? FillVolume { get; set; }
        [JsonPropertyName("notional")]          public decimal? Notional { get; set; }
        [JsonPropertyName("avg_price")]         public decimal? AvgPrice { get; set; }
        [JsonPropertyName("price")]             public decimal? Price { get; set; }
        [JsonPropertyName("fill_state")]        public string? FillState { get; set; }
        [JsonPropertyName("state")]             public string? State { get; set; }
        [JsonPropertyName("ext_dealid")]        public long? ExtDealId { get; set; }
    }

    private void HandleWsMessage(string json)
    {
        // Centroid sends { "live_trades": [...] } plus periodic status frames we ignore.
        if (string.IsNullOrWhiteSpace(json)) return;
        if (!json.Contains("live_trades")) return;

        var env = JsonSerializer.Deserialize<LiveTradesEnvelope>(json, JsonOpts);
        if (env?.LiveTrades == null) return;

        foreach (var t in env.LiveTrades)
        {
            var deal = NormalizeLiveTrade(t);
            if (deal != null) Publish(deal);
        }
    }

    private static BridgeDeal? NormalizeLiveTrade(LiveTrade t)
    {
        if (string.IsNullOrEmpty(t.CenOrdId) || string.IsNullOrEmpty(t.Symbol)) return null;

        var side = string.Equals(t.SideValue, "BUY", StringComparison.OrdinalIgnoreCase)
            ? BridgeSide.BUY
            : BridgeSide.SELL;

        var fillVol = t.FillVolume ?? t.Volume ?? 0m;
        if (fillVol <= 0m) return null; // Only interested in actual fills.

        var timeUtc = ParseUtc(t.RecvTime) ?? DateTime.UtcNow;

        // Use cen_ord_id + ext_order as a stable per-fill id.
        var dealId = !string.IsNullOrEmpty(t.NodeAccount)
            ? $"{t.CenOrdId}|{t.ExtOrder}|{t.NodeAccount}"
            : $"{t.CenOrdId}|{t.ExtOrder}";

        // CLIENT/COV_OUT classification by group is applied downstream in BridgeExecutionWorker
        // (reads the regexes from config). Here we just start from UNCLASSIFIED.
        return new BridgeDeal
        {
            DealId = dealId,
            CenOrdId = t.CenOrdId!,
            Symbol = t.Symbol!,
            Source = BridgeSource.UNCLASSIFIED,
            Side = side,
            Volume = fillVol,
            Price = t.AvgPrice ?? t.Price ?? 0m,
            TimeUtc = timeUtc,
            MtLogin = t.ExtLogin is long l and > 0 ? (ulong)l : null,
            MtGroup = t.ExtGroup,
            MtTicket = t.ExtOrder is long o and > 0 ? (ulong)o : null,
            MtDealId = t.ExtDealId is long d and > 0 ? (ulong)d : null,
        };
    }

    // Centroid ships timestamps in several formats depending on the endpoint:
    //   live_trades:   "16-04-2026 16:35:55.285580"  (DD-MM-YYYY HH:mm:ss.ffffff, UTC)
    //   maker_orders:  same
    //   ISO 8601 fallback for anything else.
    private static readonly string[] CentroidTimeFormats =
    {
        "dd-MM-yyyy HH:mm:ss.ffffff",
        "dd-MM-yyyy HH:mm:ss.fff",
        "dd-MM-yyyy HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.fffZ",
        "yyyy-MM-ddTHH:mm:ssZ",
    };

    private static DateTime? ParseUtc(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        // Try Centroid's declared formats first (exact match).
        if (DateTime.TryParseExact(s, CentroidTimeFormats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal |
                System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var exact))
            return DateTime.SpecifyKind(exact, DateTimeKind.Utc);

        // Generic ISO fallback.
        if (DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal |
                System.Globalization.DateTimeStyles.AssumeUniversal, out var dt))
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return null;
    }

    private void Publish(BridgeDeal deal)
    {
        _buffer.Add(deal);
        _msgCount++;
        _lastMsgUtc = deal.TimeUtc;
        if (_buffer.Count > 100_000)
        {
            var keep = _buffer.OrderByDescending(d => d.TimeUtc).Take(50_000).ToList();
            while (_buffer.TryTake(out _)) { }
            foreach (var k in keep) _buffer.Add(k);
        }

        List<Action<BridgeDeal>> snapshot;
        lock (_subLock) snapshot = _subscribers.ToList();
        foreach (var s in snapshot)
        {
            try { s(deal); }
            catch (Exception ex) { _logger.LogWarning(ex, "Bridge subscriber threw"); }
        }
    }

    private static string TrimBody(string body) =>
        body.Length > 200 ? body[..200] + "…" : body;

    private sealed class Unsubscribe : IDisposable
    {
        private readonly Action _a;
        private bool _done;
        public Unsubscribe(Action a) { _a = a; }
        public void Dispose()
        {
            if (_done) return;
            _done = true;
            try { _a(); } catch { /* ignore */ }
        }
    }
}
