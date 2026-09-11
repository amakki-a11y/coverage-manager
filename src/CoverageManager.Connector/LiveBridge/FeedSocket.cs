using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CoverageManager.Connector.LiveBridge;

/// <summary>The bridge refused the connection, with the HTTP status when the refusal came before the upgrade.</summary>
public sealed class FeedConnectException : Exception
{
    public FeedConnectException(HttpStatusCode? status, string message, Exception? inner = null) : base(message, inner)
    {
        Status = status;
    }

    public HttpStatusCode? Status { get; }
}

/// <summary>
/// One WebSocket to the feed: the dial with the bearer key (and an optional certificate pin), whole text frames in and
/// out, and the close handshake. Receives may run concurrently with sends; a send at a time.
/// </summary>
public sealed class FeedSocket : IDisposable
{
    private readonly ClientWebSocket _ws;
    private readonly SemaphoreSlim _sendGate = new(1, 1);

    private FeedSocket(ClientWebSocket ws) => _ws = ws;

    public WebSocketState State => _ws.State;

    public static async Task<FeedSocket> ConnectAsync(Uri uri, string apiKey, string certificateThumbprint, TimeSpan timeout, CancellationToken ct)
    {
        var ws = new ClientWebSocket();
        ws.Options.CollectHttpResponseDetails = true;
        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);
        ws.Options.SetRequestHeader("Authorization", "Bearer " + apiKey);
        if (!string.IsNullOrWhiteSpace(certificateThumbprint))
        {
            var pin = certificateThumbprint.Replace(" ", "").Replace(":", "").ToUpperInvariant();
            ws.Options.RemoteCertificateValidationCallback = (_, certificate, _, _) =>
            {
                if (certificate is null) return false;
                var c2 = certificate as X509Certificate2 ?? new X509Certificate2(certificate);
                return string.Equals(c2.Thumbprint, pin, StringComparison.OrdinalIgnoreCase);
            };
        }

        using var timed = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timed.CancelAfter(timeout);
        try
        {
            await ws.ConnectAsync(uri, timed.Token).ConfigureAwait(false);
        }
        catch (WebSocketException ex)
        {
            var status = ws.HttpStatusCode == 0 ? (HttpStatusCode?)null : ws.HttpStatusCode;
            ws.Dispose();
            throw new FeedConnectException(status, ex.Message, ex);
        }
        catch (OperationCanceledException) when (timed.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            ws.Dispose();
            throw new TimeoutException($"no connection within {timeout.TotalSeconds:F0} s");
        }
        catch
        {
            ws.Dispose();
            throw;
        }
        return new FeedSocket(ws);
    }

    /// <summary>
    /// One whole text frame; null when the bridge closed. A <see cref="TimeoutException"/> when nothing arrives within
    /// <paramref name="silenceLimit"/> (zero = wait for ever) - the socket is unusable after that, by design.
    /// </summary>
    public async Task<string?> ReceiveTextAsync(TimeSpan silenceLimit, CancellationToken ct)
    {
        using var timed = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (silenceLimit > TimeSpan.Zero) timed.CancelAfter(silenceLimit);
        var buffer = new byte[64 * 1024];
        using var message = new MemoryStream();
        try
        {
            while (true)
            {
                var result = await _ws.ReceiveAsync(buffer, timed.Token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close) return null;
                message.Write(buffer, 0, result.Count);
                if (message.Length > 16 * 1024 * 1024) throw new FeedProtocolException("a frame larger than 16 MB");
                if (result.EndOfMessage) break;
            }
        }
        catch (OperationCanceledException) when (timed.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException($"no frame within {silenceLimit.TotalSeconds:F0} s");
        }
        return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
    }

    public async Task SendTextAsync(string text, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await _sendGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    /// <summary>Sends the close frame while a receive may still be pending; the bridge answers and the receive returns null.</summary>
    public async Task BeginCloseAsync(string reason)
    {
        if (_ws.State != WebSocketState.Open) return;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, Trim(reason), cts.Token).ConfigureAwait(false);
        }
        catch (Exception) { }
    }

    /// <summary>Completes the close handshake from this side, whoever started it. Only when no receive is pending.</summary>
    public async Task CompleteCloseAsync(string reason)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            if (_ws.State == WebSocketState.CloseReceived)
                await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, Trim(reason), cts.Token).ConfigureAwait(false);
            else if (_ws.State == WebSocketState.Open)
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, Trim(reason), cts.Token).ConfigureAwait(false);
        }
        catch (Exception) { }
    }

    private static string Trim(string reason) => reason.Length > 120 ? reason[..120] : reason;

    public void Dispose()
    {
        try { _ws.Dispose(); } catch (Exception) { }
        _sendGate.Dispose();
    }
}
