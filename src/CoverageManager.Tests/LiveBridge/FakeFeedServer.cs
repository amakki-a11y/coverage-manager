using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace CoverageManager.Tests.LiveBridge;

/// <summary>
/// A loopback stand-in for the bridge's feed listener that speaks the consumer contract (TheBridge spec section 15,
/// proposal 14): the WebSocket upgrade with the bearer key on <c>/feed/&lt;source&gt;</c> (401 / 404 otherwise), then
/// whatever frames a test scripts - hello, snapshot, replay, replay_gap, the end frames, records, heartbeat, bye - and
/// it records what the consumer sends (the subscribe, the acks, the close).
/// </summary>
internal sealed class FakeFeedServer : IAsyncDisposable
{
    public const string Source = "BBcorp-Live";
    public const string DefaultKey = "test-key-0123456789";

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Channel<FakeFeedConnection> _connections = Channel.CreateUnbounded<FakeFeedConnection>();
    private readonly List<FakeFeedConnection> _all = new();
    private int _accepted;
    private int _refused;

    public FakeFeedServer(string requiredKey = DefaultKey)
    {
        RequiredKey = requiredKey;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = Task.Run(AcceptLoopAsync);
    }

    public string RequiredKey { get; set; }
    public int Port { get; }
    public string Url => $"ws://127.0.0.1:{Port}/feed/{Source}";
    public int Accepted => Volatile.Read(ref _accepted);
    public int Refused => Volatile.Read(ref _refused);

    /// <summary>The next consumer connection that completed the upgrade.</summary>
    public async Task<FakeFeedConnection> NextConnectionAsync(int timeoutMs = 5000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        try { return await _connections.Reader.ReadAsync(cts.Token); }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"no consumer connection within {timeoutMs} ms (accepted {Accepted}, refused {Refused})");
        }
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(_cts.Token); }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
            _ = Task.Run(() => HandleAsync(client));
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        try
        {
            var stream = client.GetStream();
            var request = await ReadRequestAsync(stream);
            if (request is null) { client.Dispose(); return; }
            var (target, headers) = request.Value;

            headers.TryGetValue("authorization", out var auth);
            var key = auth is not null && auth.StartsWith("Bearer ", StringComparison.Ordinal) ? auth["Bearer ".Length..].Trim() : "";
            if (key != RequiredKey) { await RefuseAsync(stream, "401 Unauthorized"); client.Dispose(); Interlocked.Increment(ref _refused); return; }
            if (target != $"/feed/{Source}") { await RefuseAsync(stream, "404 Not Found"); client.Dispose(); Interlocked.Increment(ref _refused); return; }
            if (!headers.TryGetValue("sec-websocket-key", out var wsKey)) { await RefuseAsync(stream, "400 Bad Request"); client.Dispose(); Interlocked.Increment(ref _refused); return; }

            var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(wsKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            var response = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n";
            var bytes = Encoding.ASCII.GetBytes(response);
            await stream.WriteAsync(bytes);
            await stream.FlushAsync();

            var ws = WebSocket.CreateFromStream(stream, isServer: true, subProtocol: null, keepAliveInterval: TimeSpan.FromSeconds(30));
            var connection = new FakeFeedConnection(ws, client, headers);
            lock (_all) _all.Add(connection);
            Interlocked.Increment(ref _accepted);
            _connections.Writer.TryWrite(connection);
        }
        catch (Exception)
        {
            client.Dispose();
        }
    }

    private static async Task<(string Target, Dictionary<string, string> Headers)?> ReadRequestAsync(NetworkStream stream)
    {
        using var cts = new CancellationTokenSource(5000);
        var bytes = new List<byte>(1024);
        var one = new byte[1];
        while (true)
        {
            var n = await stream.ReadAsync(one, cts.Token);
            if (n == 0) return null;
            bytes.Add(one[0]);
            var c = bytes.Count;
            if (c >= 4 && bytes[c - 4] == '\r' && bytes[c - 3] == '\n' && bytes[c - 2] == '\r' && bytes[c - 1] == '\n') break;
            if (c > 64 * 1024) return null;
        }
        var text = Encoding.ASCII.GetString(bytes.ToArray());
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return null;
        var parts = lines[0].Split(' ');
        var target = parts.Length >= 2 ? parts[1] : "";
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            headers[line[..colon].Trim().ToLowerInvariant()] = line[(colon + 1)..].Trim();
        }
        return (target, headers);
    }

    private static async Task RefuseAsync(NetworkStream stream, string status)
    {
        var bytes = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch (Exception) { }
        List<FakeFeedConnection> all;
        lock (_all) all = _all.ToList();
        foreach (var c in all) await c.DisposeAsync();
    }
}

/// <summary>One accepted consumer connection: what it sent, and the frames a test sends to it.</summary>
internal sealed class FakeFeedConnection : IAsyncDisposable
{
    private readonly WebSocket _ws;
    private readonly TcpClient _client;
    private readonly TaskCompletionSource<JsonElement> _subscribe = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<JsonElement> _acks = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);

    internal FakeFeedConnection(WebSocket ws, TcpClient client, IReadOnlyDictionary<string, string> requestHeaders)
    {
        _ws = ws;
        _client = client;
        RequestHeaders = requestHeaders;
        _ = Task.Run(ReadLoopAsync);
    }

    public IReadOnlyDictionary<string, string> RequestHeaders { get; }

    /// <summary>The consumer's first frame (its subscribe).</summary>
    public Task<JsonElement> Subscribe => _subscribe.Task;

    /// <summary>Completes when the consumer closed the socket (or the line died).</summary>
    public Task Closed => _closed.Task;

    public IReadOnlyList<JsonElement> Acks
    {
        get { lock (_acks) return _acks.ToList(); }
    }

    public bool IsOpen => _ws.State == WebSocketState.Open;

    private async Task ReadLoopAsync()
    {
        var buffer = new byte[64 * 1024];
        try
        {
            while (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseSent)
            {
                using var message = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(buffer, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (_ws.State == WebSocketState.CloseReceived)
                    {
                        try { await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closed", CancellationToken.None); }
                        catch (Exception) { }
                    }
                    break;
                }

                var element = JsonDocument.Parse(Encoding.UTF8.GetString(message.ToArray())).RootElement.Clone();
                if (!_subscribe.Task.IsCompleted) _subscribe.TrySetResult(element);
                else if (element.TryGetProperty("type", out var t) && t.GetString() == "ack") lock (_acks) _acks.Add(element);
            }
        }
        catch (Exception) { }
        finally
        {
            _subscribe.TrySetException(new InvalidOperationException("the consumer closed before sending a subscribe"));
            _closed.TrySetResult(true);
        }
    }

    // ---- frames to the consumer ------------------------------------------------------------

    public async Task SendRawAsync(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await _sendGate.WaitAsync();
        try { await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None); }
        finally { _sendGate.Release(); }
    }

    public static string Now() => DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public Task HelloAsync(string mode, string source = FakeFeedServer.Source)
        => SendRawAsync($"{{\"type\":\"hello\",\"source\":\"{source}\",\"serverTime\":\"{Now()}\",\"mode\":\"{mode}\",\"filter\":{{\"streams\":[\"positions\",\"deals\",\"accounts\",\"ticks\"],\"groups\":\"everything\",\"logins\":\"everyone\",\"symbols\":\"everything\"}}}}");

    public Task SnapshotAsync(string stream, params string[] records) => BatchAsync("snapshot", stream, records);
    public Task ReplayAsync(string stream, params string[] records) => BatchAsync("replay", stream, records);
    public Task RecordsAsync(string stream, params string[] records) => BatchAsync("records", stream, records);

    private Task BatchAsync(string type, string stream, string[] records)
        => SendRawAsync($"{{\"type\":\"{type}\",\"stream\":\"{stream}\",\"records\":[{string.Join(",", records)}]}}");

    public Task SnapshotEndAsync(long positions, long deals, long accounts, long ticks) => EndAsync("snapshot_end", positions, deals, accounts, ticks);
    public Task ReplayEndAsync(long positions, long deals, long accounts, long ticks) => EndAsync("replay_end", positions, deals, accounts, ticks);

    private Task EndAsync(string type, long positions, long deals, long accounts, long ticks)
        => SendRawAsync($"{{\"type\":\"{type}\",\"seq\":{{\"positions\":{positions},\"deals\":{deals},\"accounts\":{accounts},\"ticks\":{ticks}}}}}");

    public Task ReplayGapAsync(string stream, string reason)
        => SendRawAsync($"{{\"type\":\"replay_gap\",\"stream\":\"{stream}\",\"reason\":\"{reason}\"}}");

    public Task RecordAsync(string stream, string action, long seq, string payload)
        => SendRawAsync("{\"type\":\"record\"," + RecordBody(stream, action, seq, payload) + "}");

    /// <summary>A record element for a snapshot / replay / records batch.</summary>
    public static string Record(string stream, string action, long seq, string payload) => "{" + RecordBody(stream, action, seq, payload) + "}";

    private static string RecordBody(string stream, string action, long seq, string payload)
        => $"\"source\":\"{FakeFeedServer.Source}\",\"stream\":\"{stream}\",\"action\":\"{action}\",\"seq\":{seq},\"at\":\"{Now()}\",\"payload\":{payload}";

    public Task HeartbeatAsync(string source = "connected", long positions = 0, long deals = 0, long accounts = 0, long ticks = 0)
        => SendRawAsync($"{{\"type\":\"heartbeat\",\"at\":\"{Now()}\",\"source\":\"{source}\",\"seq\":{{\"positions\":{positions},\"deals\":{deals},\"accounts\":{accounts},\"ticks\":{ticks}}}}}");

    /// <summary>The bye frame, then the bridge's close frame (the bridge's way of dropping a consumer).</summary>
    public async Task ByeAsync(string reason)
    {
        await SendRawAsync($"{{\"type\":\"bye\",\"reason\":\"{reason}\"}}");
        try { await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, reason, CancellationToken.None); }
        catch (Exception) { }
    }

    /// <summary>The whole snapshot handshake in the contract's order: hello, positions, deals (empty), accounts, ticks, snapshot_end.</summary>
    public async Task SnapshotHandshakeAsync(string[] accounts, string[] positions, string[] ticks, long seqPositions, long seqDeals, long seqAccounts, long seqTicks)
    {
        await HelloAsync("snapshot");
        await SnapshotAsync("positions", positions);
        await SnapshotAsync("deals");
        await SnapshotAsync("accounts", accounts);
        await SnapshotAsync("ticks", ticks);
        await SnapshotEndAsync(seqPositions, seqDeals, seqAccounts, seqTicks);
    }

    /// <summary>Kills the line without a close frame (a dead network path).</summary>
    public void Abort()
    {
        try { _ws.Abort(); } catch (Exception) { }
        try { _client.Dispose(); } catch (Exception) { }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_ws.State == WebSocketState.Open)
            {
                using var cts = new CancellationTokenSource(1000);
                await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "server disposed", cts.Token);
            }
        }
        catch (Exception) { }
        Abort();
        _ws.Dispose();
    }
}

/// <summary>Payloads with the contract's keys in the contract's order (FeedPayloads in the bridge), as JSON text.</summary>
internal static class FeedPayloadJson
{
    public static string Position(long position, long login, string symbol, int action, decimal lots, double priceOpen, double priceCurrent,
                                  double profit, double storage, long timeCreate, long? closingDeal = null)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteNumber("Position", position);
            w.WriteNumber("Login", login);
            w.WriteString("Symbol", symbol);
            w.WriteNumber("Action", action);
            w.WriteNumber("VolumeExt", (long)(lots * 100_000_000m));
            w.WriteNumber("PriceOpen", priceOpen);
            w.WriteNumber("PriceCurrent", priceCurrent);
            w.WriteNumber("PriceSL", 0d);
            w.WriteNumber("PriceTP", 0d);
            w.WriteNumber("Profit", profit);
            w.WriteNumber("Storage", storage);
            w.WriteNumber("TimeCreate", timeCreate);
            w.WriteNumber("RateMargin", 1d);
            w.WriteNumber("ContractSize", 100d);
            w.WriteNumber("Digits", 2);
            w.WriteNumber("Dealer", 0L);
            w.WriteNumber("ModifyFlags", 0);
            if (closingDeal is { } cd) w.WriteNumber("ClosingDeal", cd);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public static string Deal(long deal, long login, string symbol, int action, int entry, decimal lots, double price, double profit,
                              double storage, double commission, long time, long order, long positionId, string? comment = null)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteNumber("Deal", deal);
            w.WriteNumber("Login", login);
            w.WriteString("Symbol", symbol);
            w.WriteNumber("Action", action);
            w.WriteNumber("Entry", entry);
            w.WriteNumber("VolumeExt", (long)(lots * 100_000_000m));
            w.WriteNumber("Price", price);
            w.WriteNumber("Profit", profit);
            w.WriteNumber("Storage", storage);
            w.WriteNumber("Commission", commission);
            w.WriteNumber("Time", time);
            w.WriteNumber("Order", order);
            w.WriteNumber("PositionID", positionId);
            w.WriteNumber("Reason", 0);
            if (comment is null) w.WriteNull("Comment"); else w.WriteString("Comment", comment);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public static string Account(long login, string group, int leverage, string currency, double balance, double credit,
                                 double? equity, double? floating, double? margin, double? marginFree, bool computed, string? name = null)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteNumber("Login", login);
            w.WriteString("Group", group);
            w.WriteNumber("Leverage", leverage);
            w.WriteString("Currency", currency);
            w.WriteNumber("Balance", balance);
            w.WriteNumber("Credit", credit);
            Nullable(w, "Equity", equity);
            Nullable(w, "Floating", floating);
            Nullable(w, "Margin", margin);
            Nullable(w, "MarginFree", marginFree);
            w.WriteBoolean("Computed", computed);
            if (computed) w.WriteString("ComputedAt", FakeFeedConnection.Now()); else w.WriteNull("ComputedAt");
            if (name is not null) w.WriteString("Name", name);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public static string Tick(string symbol, double bid, double ask, long timeMsc)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            w.WriteString("Symbol", symbol);
            w.WriteNumber("Bid", bid);
            w.WriteNumber("Ask", ask);
            w.WriteNumber("Last", 0d);
            w.WriteNumber("Volume", 0L);
            w.WriteNumber("TimeMsc", timeMsc);
            w.WriteNumber("Flags", 6L);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void Nullable(Utf8JsonWriter w, string name, double? value)
    {
        if (value is { } v) w.WriteNumber(name, v); else w.WriteNull(name);
    }
}
