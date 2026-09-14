using System.Text.Json;
using System.Text.Json.Serialization;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;

namespace CoverageManager.Api.Services;

/// <summary>Counters for /api/exposure/diagnostics.coveragePoll.</summary>
public sealed record CoveragePollStatus(
    bool Enabled,
    string CollectorUrl,
    DateTime? LastAppliedAtUtc,
    int LastAppliedCount,
    long Applies,
    long AmbiguousEmptySkipped,
    long Failures,
    int ConsecutiveFailures,
    bool Stale,
    string? LastError);

/// <summary>
/// v2's own read of the coverage (LP) positions, from the Python collector's existing
/// <c>GET /positions</c> (H2, docs/V2_PARALLEL_RUN.md). The collector pushes positions to ONE backend
/// (<c>BACKEND_URL</c>, v1) and stays untouched, so v2 pulls instead. The old
/// <c>Coverage:PollFallbackEnabled</c> key was never read by anything; this replaces it.
///
/// <para><b>The trap this is built around:</b> the collector answers <c>GET /positions</c> with <c>[]</c> both
/// when the LP book is genuinely flat AND when the MT5 call failed or timed out (<c>mt5_call</c> returns None and
/// <c>if not positions: return []</c>). Applying every <c>[]</c> would wipe all coverage on each MT5 hiccup: every
/// hedge would flash to 0% and raise false unhedged / wrong-way alarms. The push loop does not have this problem
/// (it skips a failed read), so the poller must not either:</para>
/// <list type="bullet">
/// <item>non-empty list -> MT5 answered -> apply it;</item>
/// <item>empty list -> apply only when <c>/health</c> reports <c>status = ok</c> and not stale, AND the previous poll
/// was also a health-confirmed empty. A really flat book clears within two polls; a one-off timeout never does;</item>
/// <item>HTTP error or unreadable body -> keep the last snapshot, count the failure.</item>
/// </list>
/// <para>Applying a snapshot does exactly what <c>POST /api/coverage/positions</c> does:
/// <see cref="PositionManager.UpdateCoveragePositions"/> then a dirty mark for the broadcast. <c>login</c> is taken from
/// <c>/health</c>; <c>openTime</c> is not in the GET payload, so polled coverage positions carry no open time (a known
/// difference from v1's push, which cannot be closed without changing the collector).</para>
/// </summary>
public sealed class CollectorPositionsPoller : BackgroundService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly PositionManager _positions;
    private readonly Action _markDirty;
    private readonly ILogger<CollectorPositionsPoller> _logger;
    private readonly bool _enabled;
    private readonly string _collectorUrl;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _staleAfter;

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly object _gate = new();
    private DateTime? _lastAppliedAt;
    private int _lastAppliedCount;
    private long _applies, _ambiguousSkipped, _failures;
    private int _consecutiveFailures;
    private bool _pendingConfirmedEmpty;
    private bool _staleLogged;
    private string? _lastError;

    public CollectorPositionsPoller(
        IHttpClientFactory httpFactory, PositionManager positions, ExposureBroadcastService broadcast,
        IConfiguration config, ILogger<CollectorPositionsPoller> logger)
        : this(httpFactory, positions, () => broadcast.MarkDirty(), config, logger) { }

    /// <summary>Test seam: the dirty-mark callback instead of the broadcast service.</summary>
    public CollectorPositionsPoller(
        IHttpClientFactory httpFactory, PositionManager positions, Action markDirty,
        IConfiguration config, ILogger<CollectorPositionsPoller> logger)
    {
        _httpFactory = httpFactory;
        _positions = positions;
        _markDirty = markDirty;
        _logger = logger;
        _enabled = config.GetValue("Coverage:PollEnabled", false);
        _collectorUrl = (config["Coverage:CollectorUrl"] ?? "http://127.0.0.1:8100").TrimEnd('/');
        _interval = TimeSpan.FromMilliseconds(Math.Max(250, config.GetValue("Coverage:PollIntervalMs", 1000)));
        _staleAfter = TimeSpan.FromMilliseconds(Math.Max(1000, config.GetValue("Coverage:PollStaleAfterMs", 15000)));
    }

    public CoveragePollStatus Status
    {
        get
        {
            lock (_gate)
            {
                var stale = _enabled && (_lastAppliedAt is null || DateTime.UtcNow - _lastAppliedAt > _staleAfter);
                return new CoveragePollStatus(_enabled, _collectorUrl, _lastAppliedAt, _lastAppliedCount, _applies,
                    _ambiguousSkipped, _failures, _consecutiveFailures, stale, _lastError);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("Coverage positions poller disabled (Coverage:PollEnabled=false); relying on collector push");
            return;
        }
        _logger.LogInformation("Coverage positions poller: GET {Url}/positions every {Interval} ms (collector untouched)",
            _collectorUrl, (int)_interval.TotalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PollOnceAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Coverage positions poll tick failed"); }

            var status = Status;
            if (status.Stale && !_staleLogged)
            {
                _staleLogged = true;
                _logger.LogWarning("Coverage positions have not been refreshed for over {Seconds} s (last error: {Error}); keeping the last snapshot",
                    (int)_staleAfter.TotalSeconds, status.LastError ?? "none");
            }
            else if (!status.Stale) _staleLogged = false;

            try { await Task.Delay(_interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }

    public enum PollOutcome { Applied, AppliedEmpty, EmptyPendingConfirmation, EmptySkippedUnhealthy, Failed }

    /// <summary>One poll. Public for tests.</summary>
    public async Task<PollOutcome> PollOnceAsync(CancellationToken ct = default)
    {
        var http = _httpFactory.CreateClient(nameof(CollectorPositionsPoller));
        http.Timeout = TimeSpan.FromSeconds(3);

        List<CollectorPosition>? list;
        try
        {
            using var res = await http.GetAsync($"{_collectorUrl}/positions", ct);
            if (!res.IsSuccessStatusCode) return Fail($"GET /positions -> {(int)res.StatusCode}");
            list = JsonSerializer.Deserialize<List<CollectorPosition>>(await res.Content.ReadAsStringAsync(ct), Json);
            if (list is null) return Fail("GET /positions returned no JSON array");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { return Fail("GET /positions failed: " + ex.Message); }

        var health = await ReadHealthAsync(http, ct);

        if (list.Count == 0)
        {
            var healthy = health is { Status: "ok", Stale: false };
            lock (_gate)
            {
                _consecutiveFailures = 0;
                if (!healthy)
                {
                    _pendingConfirmedEmpty = false;
                    _ambiguousSkipped++;
                    _lastError = "empty /positions while collector health is " + (health?.Status ?? "unreadable") + "; not applied";
                    return PollOutcome.EmptySkippedUnhealthy;
                }
                if (!_pendingConfirmedEmpty)
                {
                    _pendingConfirmedEmpty = true;   // need a second confirmed-empty poll before wiping coverage
                    _ambiguousSkipped++;
                    return PollOutcome.EmptyPendingConfirmation;
                }
            }
            Apply(Array.Empty<CoveragePositionDto>());
            return PollOutcome.AppliedEmpty;
        }

        var login = health?.Login ?? 0;
        var dtos = list.Select(p => new CoveragePositionDto
        {
            Symbol = p.Symbol ?? string.Empty,
            Direction = p.Direction ?? string.Empty,
            Volume = p.Volume,
            OpenPrice = p.OpenPrice,
            CurrentPrice = p.CurrentPrice,
            Profit = p.Profit,
            Swap = p.Swap,
            Ticket = p.Ticket,
            Login = login,
            OpenTime = null,   // not in the GET payload (see class remarks)
        }).ToList();
        Apply(dtos);
        return PollOutcome.Applied;
    }

    private void Apply(IReadOnlyCollection<CoveragePositionDto> dtos)
    {
        _positions.UpdateCoveragePositions(dtos);
        _markDirty();
        lock (_gate)
        {
            _pendingConfirmedEmpty = false;
            _lastAppliedAt = DateTime.UtcNow;
            _lastAppliedCount = dtos.Count;
            _applies++;
            _consecutiveFailures = 0;
            _lastError = null;
        }
    }

    private PollOutcome Fail(string error)
    {
        lock (_gate)
        {
            _failures++;
            _consecutiveFailures++;
            _pendingConfirmedEmpty = false;
            _lastError = error;
        }
        return PollOutcome.Failed;
    }

    private async Task<CollectorHealth?> ReadHealthAsync(HttpClient http, CancellationToken ct)
    {
        try
        {
            using var res = await http.GetAsync($"{_collectorUrl}/health", ct);
            if (!res.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<CollectorHealth>(await res.Content.ReadAsStringAsync(ct), Json);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            // unreadable health == not healthy: an empty list is then never applied
            _logger.LogDebug(ex, "Collector /health unreadable");
            return null;
        }
    }

    private sealed class CollectorPosition
    {
        public string? Symbol { get; set; }
        public string? Direction { get; set; }
        public decimal Volume { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal Profit { get; set; }
        public decimal Swap { get; set; }
        public long Ticket { get; set; }
    }

    private sealed class CollectorHealth
    {
        public string? Status { get; set; }
        public bool Stale { get; set; }
        public long? Login { get; set; }
        [JsonPropertyName("last_position_update_utc")] public string? LastPositionUpdateUtc { get; set; }
    }
}
