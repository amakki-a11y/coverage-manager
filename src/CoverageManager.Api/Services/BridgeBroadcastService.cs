using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CoverageManager.Core.Models.Bridge;

namespace CoverageManager.Api.Services;

/// <summary>
/// WebSocket hub for /ws/bridge. Pushes ExecutionPair updates to connected clients.
/// Simpler than ExposureBroadcastService — pair updates are low-rate (one per deal),
/// so we broadcast every event directly.
/// </summary>
public class BridgeBroadcastService
{
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
    private readonly ILogger<BridgeBroadcastService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public BridgeBroadcastService(ILogger<BridgeBroadcastService> logger)
    {
        _logger = logger;
    }

    public void AddClient(string id, WebSocket ws)
    {
        _clients[id] = ws;
        _logger.LogInformation("Bridge WS client connected: {Id} ({Total} total)", id, _clients.Count);
    }

    public void RemoveClient(string id)
    {
        _clients.TryRemove(id, out _);
        _logger.LogInformation("Bridge WS client disconnected: {Id} ({Total} remaining)", id, _clients.Count);
    }

    public async Task BroadcastPairAsync(ExecutionPair pair)
    {
        if (_clients.IsEmpty) return;

        var payload = JsonSerializer.Serialize(new { type = "pair", pair }, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(payload);
        var segment = new ArraySegment<byte>(bytes);

        var dead = new List<string>();
        foreach (var (id, ws) in _clients)
        {
            if (ws.State != WebSocketState.Open) { dead.Add(id); continue; }
            try
            {
                await ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bridge WS send to {Id} failed — removing", id);
                dead.Add(id);
            }
        }
        foreach (var id in dead) _clients.TryRemove(id, out _);
    }
}
