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
    private string _mode = "Stub";

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

        // 2. Pick a new implementation. Any new mode needs a line here.
        ICentroidBridgeService svc = string.Equals(mode, "Live", StringComparison.OrdinalIgnoreCase)
            ? _services.GetRequiredService<RestCentroidBridgeService>()
            : _services.GetRequiredService<StubCentroidBridgeService>();

        // 3. Pipe its deals into our subscribers so downstream listeners don't reconnect.
        var sub = svc.Subscribe(FanOut);

        // 4. Start it if it's an IHostedService (Stub + Fix both are).
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
