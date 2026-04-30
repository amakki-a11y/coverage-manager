using System.Net.WebSockets;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;

namespace CoverageManager.Api.Services;

/// <summary>
/// Broadcasts exposure updates to all connected WebSocket clients.
/// Throttles to max N updates per second.
/// </summary>
public class ExposureBroadcastService : IDisposable
{
    private readonly ExposureEngine _exposureEngine;
    private readonly PriceCache _priceCache;
    private readonly DealStore _dealStore;
    private readonly AlertEngine _alertEngine;
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
    private readonly ILogger<ExposureBroadcastService> _logger;
    private readonly Timer _broadcastTimer;
    private readonly Timer _priceTimer;
    private readonly int _maxUpdatesPerSecond;
    private readonly int _priceUpdatesPerSecond;
    private bool _dirty = true;
    private bool _priceDirty = false;
    private long _broadcastCount;
    private long _priceBroadcastCount;
    private long _droppedPriceTicks;

    public long BroadcastCount => Interlocked.Read(ref _broadcastCount);
    public long PriceBroadcastCount => Interlocked.Read(ref _priceBroadcastCount);
    public long DroppedPriceTicks => Interlocked.Read(ref _droppedPriceTicks);

    // Callback to persist new alerts to Supabase
    private Func<IEnumerable<AlertEvent>, Task>? _onNewAlerts;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExposureBroadcastService(
        ExposureEngine exposureEngine,
        PriceCache priceCache,
        DealStore dealStore,
        AlertEngine alertEngine,
        IConfiguration config,
        ILogger<ExposureBroadcastService> logger)
    {
        _exposureEngine = exposureEngine;
        _priceCache = priceCache;
        _dealStore = dealStore;
        _alertEngine = alertEngine;
        _logger = logger;
        _maxUpdatesPerSecond = config.GetValue("WebSocket:MaxUpdatesPerSecond", 10);
        // Price-only fast path is intentionally faster than the full state
        // broadcast — the price payload is tiny (~symbol+bid+ask+ts per quote)
        // so we can push at 20 Hz without cooking the browser. Full-state
        // broadcasts stay at _maxUpdatesPerSecond because they carry the
        // exposure recompute, deal P&L, alerts, etc.
        _priceUpdatesPerSecond = config.GetValue("WebSocket:PriceUpdatesPerSecond", 20);

        var interval = 1000 / _maxUpdatesPerSecond;
        _broadcastTimer = new Timer(BroadcastIfDirty, null, interval, interval);

        var priceInterval = 1000 / _priceUpdatesPerSecond;
        _priceTimer = new Timer(BroadcastPricesIfDirty, null, priceInterval, priceInterval);
    }

    public void SetAlertPersistCallback(Func<IEnumerable<AlertEvent>, Task> callback)
    {
        _onNewAlerts = callback;
    }

    public void AddClient(string id, WebSocket socket)
    {
        _clients[id] = socket;
        _logger.LogInformation("WebSocket client connected: {Id}. Total: {Count}", id, _clients.Count);
        _dirty = true; // Send initial state
    }

    public void RemoveClient(string id)
    {
        _clients.TryRemove(id, out _);
        _logger.LogInformation("WebSocket client disconnected: {Id}. Total: {Count}", id, _clients.Count);
    }

    public void MarkDirty() => _dirty = true;

    /// <summary>
    /// Lightweight signal from the MT5 tick path. Coalesces all ticks that
    /// arrive between price-broadcast frames into a single send, so a noisy
    /// symbol can't queue up hundreds of WS messages. Use this instead of
    /// MarkDirty() for tick events — MarkDirty triggers the heavy
    /// exposure-recompute broadcast.
    /// </summary>
    public void MarkPriceDirty(string? symbol = null)
    {
        if (_priceDirty)
            Interlocked.Increment(ref _droppedPriceTicks);
        _priceDirty = true;
    }

    /// <summary>
    /// Lightweight price-only broadcast. Sends the latest bid/ask, a per-symbol
    /// floating P&amp;L decomposition (B-Book + Coverage sums), and a server
    /// timestamp so the dealer UI can flag stale prices.
    ///
    /// <para>Phase 2.16 (price_update v1) shipped only the bid/ask. That fixed
    /// the "price under symbol" cell freeze but left every floating-P&amp;L
    /// surface (Exposure open-row P&amp;L, Net P&amp;L tab "Current", Topbar
    /// "Net P&amp;L Today" tile) gated on the slower full-state broadcast
    /// (~7 Hz, position-event-driven). Phase 2.17 adds the per-canonical-symbol
    /// floating sum from <see cref="ExposureEngine.GetFloatingPnLPerSymbol"/>
    /// (~10× cheaper than the full <c>CalculateExposure</c>) so the frontend
    /// overlay updates all three surfaces at full tick cadence (~20 Hz)
    /// without paying the heavy aggregation cost on every frame.</para>
    /// </summary>
    private async void BroadcastPricesIfDirty(object? state)
    {
        if (!_priceDirty || _clients.IsEmpty) return;
        _priceDirty = false;

        try
        {
            var prices = _priceCache.GetAll();
            // Phase 2.17: skinny per-symbol floating P&L. Cheap relative to
            // the full ExposureEngine.CalculateExposure() — no canonical
            // grouping, no weighted avg, no hedge ratio. Frontend overlays
            // these onto exposureSummaries[].
            var floatingPnls = _exposureEngine.GetFloatingPnLPerSymbol();
            Interlocked.Increment(ref _priceBroadcastCount);

            var message = new
            {
                type = "price_update",
                data = new
                {
                    prices,
                    floatingPnls,
                    timestamp = DateTime.UtcNow
                }
            };

            var json = JsonSerializer.Serialize(message, JsonOptions);
            var buffer = Encoding.UTF8.GetBytes(json);

            var deadClients = new List<string>();

            foreach (var (id, socket) in _clients)
            {
                try
                {
                    if (socket.State == WebSocketState.Open)
                    {
                        await socket.SendAsync(
                            new ArraySegment<byte>(buffer),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None);
                    }
                    else
                    {
                        deadClients.Add(id);
                    }
                }
                catch
                {
                    deadClients.Add(id);
                }
            }

            foreach (var id in deadClients)
                RemoveClient(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting price update");
        }
    }

    private async void BroadcastIfDirty(object? state)
    {
        if (!_dirty || _clients.IsEmpty) return;
        _dirty = false;

        try
        {
            Interlocked.Increment(ref _broadcastCount);
            var exposure = _exposureEngine.CalculateExposure();
            var prices = _priceCache.GetAll();
            var pnl = _dealStore.GetPnLBySymbol();

            // Evaluate alert thresholds
            var newAlerts = _alertEngine.Evaluate();
            if (newAlerts.Count > 0)
            {
                _logger.LogWarning("Fired {Count} new alerts", newAlerts.Count);
                // Persist to Supabase asynchronously
                if (_onNewAlerts != null)
                    _ = _onNewAlerts(newAlerts);
            }

            var message = new
            {
                type = "exposure_update",
                data = new
                {
                    exposure,
                    prices,
                    pnl,
                    alerts = newAlerts.Count > 0 ? newAlerts : null,
                    alertCount = _alertEngine.ActiveAlertCount,
                    totalDeals = _dealStore.DealCount,
                    timestamp = DateTime.UtcNow
                }
            };

            var json = JsonSerializer.Serialize(message, JsonOptions);
            var buffer = Encoding.UTF8.GetBytes(json);

            var deadClients = new List<string>();

            foreach (var (id, socket) in _clients)
            {
                try
                {
                    if (socket.State == WebSocketState.Open)
                    {
                        await socket.SendAsync(
                            new ArraySegment<byte>(buffer),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None);
                    }
                    else
                    {
                        deadClients.Add(id);
                    }
                }
                catch
                {
                    deadClients.Add(id);
                }
            }

            foreach (var id in deadClients)
                RemoveClient(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting exposure update");
        }
    }

    public void Dispose()
    {
        _broadcastTimer.Dispose();
        _priceTimer.Dispose();
    }
}
