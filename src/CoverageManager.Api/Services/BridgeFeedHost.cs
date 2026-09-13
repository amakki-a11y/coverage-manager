using CoverageManager.Core.Models.Bridge;

namespace CoverageManager.Api.Services;

/// <summary>
/// Runtime container for the active ICentroidBridgeService. Supports hot-swapping between
/// Stub and Live (FIX 4.4) modes when the user saves new settings in the UI.
///
/// Why not just resolve ICentroidBridgeService from DI? Because DI is decided once at
/// startup — we need to tear down the old feed (e.g. close the FIX session) and start a new
/// one in-process when settings change.
/// </summary>
public class BridgeFeedHost : IAsyncDisposable
{
    private readonly IServiceProvider _services;
    private readonly ILogger<BridgeFeedHost> _logger;
    private readonly List<Action<BridgeDeal>> _subscribers = new();
    private readonly object _lock = new();

    private ICentroidBridgeService? _active;
    private IHostedService? _activeAsHosted;
    private IDisposable? _activeSubscription;
    private CancellationTokenSource? _activeCts;
    /// <summary>Mode name used whenever no Centroid feed is running. The v2 default.</summary>
    public const string DormantMode = "Disabled";

    private string _mode = DormantMode;

    public BridgeFeedHost(IServiceProvider services, ILogger<BridgeFeedHost> logger)
    {
        _services = services;
        _logger = logger;
    }

    public string CurrentMode => _mode;

    public CentroidHealth GetHealth()
    {
        var a = _active;
        return a?.GetHealth() ?? new CentroidHealth
        {
            Mode = _mode,
            State = CentroidConnectionState.Disconnected,
        };
    }

    public Task<IReadOnlyList<BridgeDeal>> GetDealsAsync(
        DateTime fromUtc, DateTime toUtc, string? canonicalSymbol, CancellationToken ct)
    {
        var a = _active;
        return a?.GetDealsAsync(fromUtc, toUtc, canonicalSymbol, ct)
               ?? Task.FromResult<IReadOnlyList<BridgeDeal>>(Array.Empty<BridgeDeal>());
    }

    public IDisposable Subscribe(Action<BridgeDeal> onDeal)
    {
        lock (_lock) _subscribers.Add(onDeal);
        return new Unsubscribe(() =>
        {
            lock (_lock) _subscribers.Remove(onDeal);
        });
    }

    public ClientOrderDetail? GetClientDetail(string cenOrdId)
        => _active?.GetClientDetail(cenOrdId);

    /// <summary>
    /// Switch the backing feed. Safe to call at runtime.
    /// </summary>
    public async Task SwitchAsync(string mode)
    {
        _logger.LogInformation("BridgeFeedHost switching mode → {Mode}", mode);

        // 1. Tear down the current feed.
        await StopActiveAsync();

        // 2. Pick a new implementation. Live (real Centroid dropcopy) is the ONLY feed in v2.
        //    The synthetic Stub was retired: in v1 it ran in production from 2026-04-16 and
        //    wrote 85,000+ fabricated pairs into the store, of which only 1,943 were real.
        //    Anything that is not "Live" leaves the host DORMANT (_active stays null, which
        //    every accessor here already handles), so the tab and pairing engine survive
        //    untouched but no synthetic data can ever be produced.
        if (!string.Equals(mode, "Live", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(mode, "Stub", StringComparison.OrdinalIgnoreCase))
            {
                // A legacy bridge_settings row may still say "Stub"; do not crash on it.
                _logger.LogWarning("Centroid mode 'Stub' was retired in v2; the feed stays dormant.");
            }
            lock (_lock) { _mode = DormantMode; }
            _logger.LogInformation("BridgeFeedHost dormant (no Centroid feed running)");
            return;
        }

        ICentroidBridgeService svc = _services.GetRequiredService<RestCentroidBridgeService>();

        // 3. Pipe its deals into our subscribers so downstream listeners don't reconnect.
        var sub = svc.Subscribe(FanOut);

        // 4. Start it if it's an IHostedService.
        var hosted = svc as IHostedService;
        var cts = new CancellationTokenSource();
        if (hosted != null)
        {
            await hosted.StartAsync(cts.Token);
        }

        lock (_lock)
        {
            _active = svc;
            _activeAsHosted = hosted;
            _activeSubscription = sub;
            _activeCts = cts;
            _mode = mode;
        }
        _logger.LogInformation("BridgeFeedHost now running {Mode}", mode);
    }

    private async Task StopActiveAsync()
    {
        IDisposable? sub;
        IHostedService? hosted;
        CancellationTokenSource? cts;
        lock (_lock)
        {
            sub = _activeSubscription; _activeSubscription = null;
            hosted = _activeAsHosted; _activeAsHosted = null;
            cts = _activeCts; _activeCts = null;
            _active = null;
        }
        try { sub?.Dispose(); } catch { /* ignore */ }
        if (hosted != null)
        {
            try
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await hosted.StopAsync(stopCts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping previous feed");
            }
        }
        try { cts?.Cancel(); } catch { /* ignore */ }
        cts?.Dispose();
    }

    private void FanOut(BridgeDeal deal)
    {
        List<Action<BridgeDeal>> snapshot;
        lock (_lock) snapshot = _subscribers.ToList();
        foreach (var s in snapshot)
        {
            try { s(deal); }
            catch (Exception ex) { _logger.LogWarning(ex, "BridgeFeedHost subscriber threw"); }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopActiveAsync();
        GC.SuppressFinalize(this);
    }

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
