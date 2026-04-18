using System.Collections.Concurrent;
using CoverageManager.Core.Models.Bridge;

namespace CoverageManager.Api.Services;

/// <summary>
/// Synthetic Centroid Dropcopy feed used when Centroid:Mode = Stub (default until credentials
/// + IP whitelist are in place). Emits a CLIENT fill every few seconds followed by one or two
/// COV_OUT legs within the pairing window.
///
/// Realistic enough to exercise the BridgePairingEngine, the persistence path, and the UI.
/// </summary>
public class StubCentroidBridgeService : BackgroundService, ICentroidBridgeService
{
    private readonly ILogger<StubCentroidBridgeService> _logger;
    private readonly ConcurrentBag<BridgeDeal> _buffer = new();
    private readonly List<Action<BridgeDeal>> _subscribers = new();
    private readonly object _subLock = new();
    private readonly Random _rng = new();
    private long _msgCount;
    private DateTime? _lastMsgUtc;

    private static readonly (string Symbol, decimal Price)[] Book =
    {
        ("XAUUSD", 4793.81m),
        ("EURUSD", 1.10000m),
        ("USDJPY", 150.00m),
        ("GBPUSD", 1.27500m),
    };

    public StubCentroidBridgeService(ILogger<StubCentroidBridgeService> logger)
    {
        _logger = logger;
    }

    public CentroidHealth GetHealth() => new()
    {
        State = CentroidConnectionState.Stubbed,
        Mode = "Stub",
        LastMessageUtc = _lastMsgUtc,
        MessagesReceived = _msgCount,
        LastError = null,
    };

    public Task<IReadOnlyList<BridgeDeal>> GetDealsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? canonicalSymbol = null,
        CancellationToken ct = default)
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

    // Stub feed has no orders_report source — always returns null so enrichment is a no-op.
    public ClientOrderDetail? GetClientDetail(string cenOrdId) => null;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StubCentroidBridgeService started (synthetic deal stream)");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var (sym, basePrice) = Book[_rng.Next(Book.Length)];
                var side = _rng.NextDouble() < 0.5 ? BridgeSide.BUY : BridgeSide.SELL;
                var volume = Math.Round((decimal)(_rng.NextDouble() * 0.9 + 0.1), 2);
                var spread = basePrice * 0.00005m;
                var clientPrice = Math.Round(basePrice + ((decimal)_rng.NextDouble() - 0.5m) * spread, 5);

                var orderId = $"ord-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                var clientTime = DateTime.UtcNow;

                var client = new BridgeDeal
                {
                    DealId = $"c-{orderId}",
                    CenOrdId = orderId,
                    Symbol = sym,
                    CanonicalSymbol = sym,
                    Source = BridgeSource.CLIENT,
                    Side = side,
                    Volume = volume,
                    Price = clientPrice,
                    TimeUtc = clientTime,
                    MtGroup = "real\\demo\\b-book",
                    MtLogin = 96900,
                };

                Publish(client);

                // 0–2 cov fills within ±1s. Occasionally zero to exercise missing-coverage path.
                var legCount = _rng.NextDouble() < 0.15 ? 0 : _rng.Next(1, 3);
                var remaining = volume;
                for (var i = 0; i < legCount && remaining > 0m; i++)
                {
                    var legVol = i == legCount - 1
                        ? remaining
                        : Math.Round(remaining * (decimal)(_rng.NextDouble() * 0.6 + 0.2), 2);
                    legVol = Math.Min(legVol, remaining);
                    if (legVol <= 0m) break;

                    var skew = side == BridgeSide.SELL
                        ? (decimal)(_rng.NextDouble() * 0.0005) * basePrice
                        : -(decimal)(_rng.NextDouble() * 0.0005) * basePrice;
                    var legPrice = Math.Round(clientPrice + skew, 5);
                    var legTime = clientTime.AddMilliseconds(_rng.Next(-200, 400));

                    Publish(new BridgeDeal
                    {
                        DealId = $"v-{orderId}-{i}",
                        CenOrdId = orderId,
                        Symbol = sym,
                        CanonicalSymbol = sym,
                        Source = BridgeSource.COV_OUT,
                        Side = side,
                        Volume = legVol,
                        Price = legPrice,
                        TimeUtc = legTime,
                        MtGroup = "real\\demo\\a-book",
                        LpName = _rng.NextDouble() < 0.5 ? "LP-Alpha" : "LP-Beta",
                    });

                    remaining -= legVol;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(_rng.Next(800, 2500)), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stub feed tick failed");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }

        _logger.LogInformation("StubCentroidBridgeService stopped");
    }

    private void Publish(BridgeDeal deal)
    {
        _buffer.Add(deal);
        _msgCount++;
        _lastMsgUtc = deal.TimeUtc;

        // Retain roughly the last 24h in memory — simple cap by count (100k deals).
        if (_buffer.Count > 100_000)
        {
            // Re-seat the bag with the newest slice. ConcurrentBag has no trim, so we snapshot.
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
