using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CoverageManager.Core.Engines;

namespace CoverageManager.Api.Services;

/// <summary>
/// Background service that periodically re-fetches active <c>symbol_mappings</c>
/// from Supabase and atomically replaces the in-memory <see cref="PositionManager"/>
/// cache.
///
/// <para><b>Why this exists:</b> on 2026-05-07 a transient TLS-layer reset
/// (<c>SocketException 10054</c>) during the cold-start Supabase fetch left the
/// in-memory mappings cache empty for the entire post-startup window. The
/// frontend rendered every symbol with the amber UNMAPPED badge until a dealer
/// happened to edit a mapping (which calls <c>SymbolMappingController</c>'s
/// reload path). Without this service, the only auto-recovery was a second
/// service restart.</para>
///
/// <para><b>Cadence:</b> first tick after a 60s startup delay, then every 60s.
/// The startup delay gives <c>Program.cs</c>'s primary mapping load (which now
/// retries 3x via <see cref="SupabaseService.GetMappingsAsync"/>) a chance to
/// succeed before this service starts ticking — keeps logs quiet on the happy
/// path. If startup's load failed, this service auto-heals within 60s.</para>
///
/// <para><b>Empty-result safety:</b> if Supabase returns an empty list
/// (typically because <c>GetMappingsAsync</c>'s retry exhausted on a transient
/// failure), <see cref="RefreshOnceAsync"/> does NOT clobber the existing cache
/// — <see cref="PositionManager.LoadMappings"/> would <c>Clear()</c> first and
/// leave us with nothing. We log a warning and bump
/// <see cref="ConsecutiveFailures"/> instead.</para>
///
/// <para><b>Side benefit:</b> picks up out-of-band mapping edits made directly
/// in Supabase within 60s, instead of requiring a service restart or a dealer
/// CRUD action via the Mappings tab.</para>
///
/// <para>Counters are surfaced in <c>/api/exposure/diagnostics.mappings</c>.
/// Registered as a singleton AND as <c>IHostedService</c> so the controller
/// can resolve the same instance via DI.</para>
/// </summary>
public sealed class MappingRefreshService : BackgroundService
{
    private readonly SupabaseService _supabase;
    private readonly PositionManager _positionManager;
    private readonly ILogger<MappingRefreshService> _logger;

    private const int StartupDelaySeconds = 60;
    private const int RefreshIntervalSeconds = 60;

    private DateTime? _lastFetchAtUtc;
    private bool _lastFetchOk;
    private int _lastFetchCount;
    private int _consecutiveFailures;

    public DateTime? LastFetchAtUtc => _lastFetchAtUtc;
    public bool LastFetchOk => _lastFetchOk;
    public int LastFetchCount => _lastFetchCount;
    public int ConsecutiveFailures => _consecutiveFailures;

    public MappingRefreshService(
        SupabaseService supabase,
        PositionManager positionManager,
        ILogger<MappingRefreshService> logger)
    {
        _supabase = supabase;
        _positionManager = positionManager;
        _logger = logger;
    }

    /// <summary>
    /// One-shot refresh — also callable from <c>Program.cs</c>'s startup block
    /// so startup and the timer share the same code path. Returns true on
    /// success (fresh non-empty mappings written to <see cref="PositionManager"/>),
    /// false on failure (cache untouched).
    /// </summary>
    public async Task<bool> RefreshOnceAsync(CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        try
        {
            var mappings = await _supabase.GetMappingsAsync().ConfigureAwait(false);
            if (mappings.Count == 0)
            {
                // GetMappingsAsync's retry exhausted, OR the table is genuinely
                // empty. We never want to overwrite a populated cache with an
                // empty list — LoadMappings does Clear() first, which would
                // surface UNMAPPED badges across the dashboard. Bump failure
                // counter and bail; the next tick will try again.
                _consecutiveFailures++;
                _lastFetchOk = false;
                _lastFetchAtUtc = nowUtc;
                _logger.LogWarning(
                    "Mapping refresh returned 0 rows (likely transient Supabase failure); keeping current cache. Consecutive failures: {N}",
                    _consecutiveFailures);
                return false;
            }

            _positionManager.LoadMappings(mappings);
            _lastFetchCount = mappings.Count;
            _lastFetchOk = true;
            _consecutiveFailures = 0;
            _lastFetchAtUtc = nowUtc;
            _logger.LogDebug("Mapping refresh ok: {Count} active mappings loaded", mappings.Count);
            return true;
        }
        catch (Exception ex)
        {
            _consecutiveFailures++;
            _lastFetchOk = false;
            _lastFetchAtUtc = nowUtc;
            _logger.LogError(ex, "Mapping refresh threw unexpectedly. Consecutive failures: {N}", _consecutiveFailures);
            return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "MappingRefreshService starting (startup delay {Delay}s, interval {Interval}s)",
            StartupDelaySeconds, RefreshIntervalSeconds);

        try { await Task.Delay(TimeSpan.FromSeconds(StartupDelaySeconds), stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(RefreshIntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await RefreshOnceAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }

        _logger.LogInformation("MappingRefreshService stopped");
    }
}
