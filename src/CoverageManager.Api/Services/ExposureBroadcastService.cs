using System.Net.WebSockets;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using CoverageManager.Core.Engines;

namespace CoverageManager.Api.Services;

/// <summary>
/// Broadcasts exposure updates to all connected WebSocket clients.
/// Throttles to max N updates per second.
/// </summary>
public class ExposureBroadcastService : IDisposable
{
    private readonly ExposureEngine _exposureEngine;
    private readonly PriceCache _priceCache;
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
    private readonly ILogger<ExposureBroadcastService> _logger;
    private readonly Timer _broadcastTimer;
    private readonly int _maxUpdatesPerSecond;
    private bool _dirty = true;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExposureBroadcastService(
        ExposureEngine exposureEngine,
        PriceCache priceCache,
        IConfiguration config,
        ILogger<ExposureBroadcastService> logger)
    {
        _exposureEngine = exposureEngine;
        _priceCache = priceCache;
        _logger = logger;
        _maxUpdatesPerSecond = config.GetValue("WebSocket:MaxUpdatesPerSecond", 10);

        var interval = 1000 / _maxUpdatesPerSecond;
        _broadcastTimer = new Timer(BroadcastIfDirty, null, interval, interval);
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

    private async void BroadcastIfDirty(object? state)
    {
        if (!_dirty || _clients.IsEmpty) return;
        _dirty = false;

        try
        {
            var exposure = _exposureEngine.CalculateExposure();
            var prices = _priceCache.GetAll();

            var message = new
            {
                type = "exposure_update",
                data = new
                {
                    exposure,
                    prices,
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
    }
}
