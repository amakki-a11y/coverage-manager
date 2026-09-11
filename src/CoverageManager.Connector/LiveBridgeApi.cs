using System.Net;
using System.Net.WebSockets;
using CoverageManager.Connector.LiveBridge;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoverageManager.Connector;

/// <summary>
/// <see cref="IMT5Api"/> over the Live Bridge consumer feed (TheBridge product spec, section 15, proposal 14): one
/// WebSocket to <c>wss://feed.connecttrader.app:5571/feed/&lt;source&gt;</c> with a bearer key, a subscribe carrying the
/// last sequence seen per stream (null on the first start), a snapshot or a replay, then live records. Selected with
/// <c>MT5:Provider = LiveBridge</c> (see <see cref="MT5ApiFactory"/>).
///
/// How each IMT5Api member maps to the feed:
/// <list type="bullet">
/// <item><b>Initialize</b>: load the durable sequences (<see cref="LiveBridgeOptions.StatePath"/>).</item>
/// <item><b>Connect</b>: dial, subscribe with the four resume sequences, apply hello + snapshot/replay + end frame.
/// The MT5 server / login / password arguments are not used: the feed is keyed by its URL path and the bearer key.
/// A first connection that fails (refused key, no route, bad handshake) returns false with the reason in
/// <see cref="LastError"/>; the caller retries with its own backoff.</item>
/// <item><b>IsConnected</b>: a live session (handshake complete, socket open). Drops are reconnected internally with
/// resume - after <see cref="LiveBridgeOptions.SilenceLimitMs"/> of silence, after a <c>bye</c>, on any socket error -
/// with a backoff of <see cref="LiveBridgeOptions.ReconnectMs"/> doubling to <see cref="LiveBridgeOptions.ReconnectMaxMs"/>.</item>
/// <item><b>SelectedAddAll</b>: no-op (the symbol filter lives in the bridge console).</item>
/// <item><b>Subscribe* / Unsubscribe*</b>: gate the events; the feed itself always streams all four.</item>
/// <item><b>OnTick / OnDealAdd / OnPositionAdd / OnPositionUpdate / OnPositionDelete / OnUserUpdate</b>: raised for every
/// record applied to the book - snapshot, replay and live - so a reconnect's replay reaches the consumer like live data.</item>
/// <item><b>GetPositions / GetUserLogins / GetUserAccount / GetTickLast</b>: answered from the in-memory book the feed
/// maintains (<see cref="FeedBook"/>); no request ever goes to the MT5 server.</item>
/// <item><b>RequestDeals</b>: answered from the deals received in this process (the replayed gap plus live), kept for
/// <see cref="LiveBridgeOptions.DealRetentionHours"/>. Deals from before the resume point are not in the feed's replay;
/// the durable record of deals stays Supabase.</item>
/// </list>
/// Contract rules implemented: records are applied idempotently by identity; a record with a sequence at or below the
/// last applied of its stream is ignored; a position update and an account frame are full state; a delete is a close;
/// a positions snapshot reconciles the book (a position missing from it is closed); sequences are kept durably and sent
/// on every connect; <c>replay_gap</c> is accepted for ticks (the last prices follow) and logged for deals; one connection
/// per key; an ack with the applied sequences every <see cref="LiveBridgeOptions.AckEveryMs"/>.
/// A deal's four money fields (Profit, Storage = swap, Commission, Fee) map one to one. Not on the wire, fixed here:
/// account registration / last-access times (0), comments ("").
/// </summary>
public sealed class LiveBridgeApi : IMT5Api, IMT5ApiDiagnostics
{
    private readonly LiveBridgeOptions _options;
    private readonly ILogger _logger;
    private readonly FeedBook _book = new();
    private readonly object _gate = new();
    private readonly long[] _applied = new long[4];
    private readonly long[] _skipped = new long[4];

    private FeedSequenceStore? _sequences;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private FeedSocket? _socket;

    private volatile bool _initialized;
    private volatile bool _disposed;
    private volatile bool _closing;
    private volatile bool _live;
    private volatile string _state = "disconnected";
    private volatile bool _ticksOn;
    private volatile bool _dealsOn;
    private volatile bool _positionsOn;
    private volatile bool _usersOn;
    private volatile string? _mode;
    private volatile string? _source;
    private volatile string? _filter;
    private volatile string? _lastBye;
    private volatile string? _lastDrop;
    private int _sourceConnected = -1;   // -1 unknown, 0 disconnected, 1 connected (the bridge's MT5 source)
    private bool _positionSnapshot;      // loop thread only: a positions snapshot is being applied

    private long _reconnects;
    private long _byes;
    private long _handshakes;
    private long _framesReceived;
    private long _unknownFrames;
    private long _unmappable;
    private long _tickGaps;
    private long _dealGaps;
    private long _lastFrameTicks;
    private long _lastHeartbeatTicks;
    private long _lastPruneTicks;

    private long _getPositionsCalls;
    private long _getUserAccountCalls;
    private long _getUserLoginsCalls;
    private long _requestDealsCalls;
    private long _tickLastCalls;

    public LiveBridgeApi(LiveBridgeOptions? options = null, ILogger? logger = null)
    {
        _options = options ?? new LiveBridgeOptions();
        _logger = logger ?? NullLogger.Instance;
    }

    public event Action<RawTick>? OnTick;
    public event Action<RawDeal>? OnDealAdd;
    public event Action<RawPosition>? OnPositionAdd;
    public event Action<RawPosition>? OnPositionUpdate;
    public event Action<RawPosition>? OnPositionDelete;
    public event Action<RawAccount>? OnUserUpdate;

    public bool IsConnected => _live && !_disposed;
    public string LastError { get; private set; } = "";

    /// <summary>disconnected, connecting, handshake, live, reconnecting.</summary>
    public string State => _state;
    public string? Mode => _mode;
    public string? Source => _source;
    public string? LastByeReason => _lastBye;
    public string? LastDropReason => _lastDrop;
    public bool? SourceConnected => Volatile.Read(ref _sourceConnected) switch { 1 => true, 0 => false, _ => null };
    public long Reconnects => Interlocked.Read(ref _reconnects);
    public long Handshakes => Interlocked.Read(ref _handshakes);
    public long TickGaps => Interlocked.Read(ref _tickGaps);
    public long DealGaps => Interlocked.Read(ref _dealGaps);
    public DateTime? LastFrameAtUtc => Ticks(Interlocked.Read(ref _lastFrameTicks));
    public FeedBook Book => _book;
    public string? StatePath => _sequences?.Path;

    public long GetPositionsCalls => Interlocked.Read(ref _getPositionsCalls);
    public long GetUserAccountCalls => Interlocked.Read(ref _getUserAccountCalls);
    public long GetUserLoginsCalls => Interlocked.Read(ref _getUserLoginsCalls);
    public long RequestDealsCalls => Interlocked.Read(ref _requestDealsCalls);
    public long TickLastCalls => Interlocked.Read(ref _tickLastCalls);

    // ---- Session ------------------------------------------------------------------------

    public bool Initialize()
    {
        ThrowIfDisposed();
        if (_initialized) { LastError = ""; return true; }
        try
        {
            var path = _options.ResolveStatePath();
            var store = new FeedSequenceStore(path, _options.Url, TimeSpan.FromMilliseconds(Math.Max(0, _options.SequenceFlushMs)), _logger);
            store.Load();
            _sequences = store;
            _initialized = true;
            LastError = "";
            _logger.LogInformation("Live Bridge: sequence state at {Path}: {Note}", path, store.LoadNote);
            return true;
        }
        catch (Exception ex)
        {
            LastError = "LiveBridgeApi: cannot prepare the sequence state: " + ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Dials the feed and waits for the handshake (hello, snapshot or replay, end frame) up to <paramref name="timeoutMs"/>.
    /// <paramref name="server"/>, <paramref name="login"/> and <paramref name="password"/> are the MT5 manager
    /// credentials the caller has; the feed does not use them and they are never logged.
    /// </summary>
    public bool Connect(string server, ulong login, string password, uint timeoutMs = 30000)
    {
        ThrowIfDisposed();
        if (!_initialized) { LastError = "LiveBridgeApi: call Initialize() first"; return false; }
        if (string.IsNullOrWhiteSpace(_options.Url)) { LastError = "LiveBridgeApi: LiveBridge:Url is not set"; return false; }
        if (!Uri.TryCreate(_options.Url, UriKind.Absolute, out var uri) || (uri.Scheme != "ws" && uri.Scheme != "wss"))
        {
            LastError = $"LiveBridgeApi: LiveBridge:Url '{_options.Url}' is not a ws:// or wss:// URL";
            return false;
        }
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            LastError = "LiveBridgeApi: LiveBridge:ApiKey is empty; set the environment variable LiveBridge__ApiKey to the key the bridge console generated";
            return false;
        }

        TaskCompletionSource<string?> first;
        lock (_gate)
        {
            if (_loop is not null) { LastError = "LiveBridgeApi: already connected"; return _live; }
            _closing = false;
            _cts = new CancellationTokenSource();
            first = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ct = _cts.Token;
            _loop = Task.Run(() => RunAsync(uri, first, ct), CancellationToken.None);
        }
        _logger.LogInformation("Live Bridge: connecting to {Url} (the MT5 server {Server} / login {Login} given by the caller are not used by the feed)",
            uri, server, login);

        if (!first.Task.Wait(TimeSpan.FromMilliseconds(Math.Max(1000, timeoutMs))))
        {
            LastError = $"LiveBridgeApi: no complete handshake within {timeoutMs} ms";
            StopLoop("handshake timeout");
            return false;
        }
        var failure = first.Task.Result;
        if (failure is not null)
        {
            LastError = failure;
            StopLoop("first connection failed");
            return false;
        }
        LastError = "";
        return _live;
    }

    public void Disconnect()
    {
        _ticksOn = false;
        _dealsOn = false;
        _positionsOn = false;
        _usersOn = false;
        StopLoop("consumer disconnecting");
    }

    private void StopLoop(string reason)
    {
        CancellationTokenSource? cts;
        Task? loop;
        FeedSocket? socket;
        lock (_gate)
        {
            cts = _cts;
            loop = _loop;
            socket = _socket;
            _cts = null;
            _loop = null;
            _closing = true;
        }
        if (cts is null)
        {
            _closing = false;
            _live = false;
            return;
        }
        if (socket is not null) _ = socket.BeginCloseAsync(reason);
        var ended = false;
        if (loop is not null)
        {
            try { ended = loop.Wait(TimeSpan.FromSeconds(2)); }
            catch (AggregateException) { ended = true; }
        }
        if (!ended)
        {
            cts.Cancel();
            if (loop is not null)
            {
                try { loop.Wait(TimeSpan.FromSeconds(5)); }
                catch (AggregateException) { }
            }
        }
        cts.Dispose();
        _closing = false;
        _live = false;
        _state = "disconnected";
        _sequences?.Flush();
    }

    // ---- Subscriptions (gates for the events; the feed streams everything) ----------------

    public bool SubscribeTicks(string symbolMask = "*")
    {
        if (!Ready()) return false;
        _ticksOn = true;   // the mask is not applied: the feed's symbol filter is the bridge console's
        return true;
    }

    public void UnsubscribeTicks() => _ticksOn = false;

    public bool SelectedAddAll() => Ready();   // the symbol selection is the bridge console's, not the consumer's

    public bool SubscribeDeals()
    {
        if (!Ready()) return false;
        _dealsOn = true;
        return true;
    }

    public void UnsubscribeDeals() => _dealsOn = false;

    public bool SubscribePositions()
    {
        if (!Ready()) return false;
        _positionsOn = true;
        return true;
    }

    public void UnsubscribePositions() => _positionsOn = false;

    public bool SubscribeUsers()
    {
        if (!Ready()) return false;
        _usersOn = true;
        return true;
    }

    public void UnsubscribeUsers() => _usersOn = false;

    private bool Ready()
    {
        if (_initialized) return true;
        LastError = "LiveBridgeApi: not initialized";
        return false;
    }

    // ---- Queries (the book) ---------------------------------------------------------------

    public List<RawPosition> GetPositions(ulong login)
    {
        Interlocked.Increment(ref _getPositionsCalls);
        return _book.Positions(login);
    }

    public ulong[] GetUserLogins(string groupMask)
    {
        Interlocked.Increment(ref _getUserLoginsCalls);
        return _book.Logins(groupMask);
    }

    public RawTick? GetTickLast(string symbol)
    {
        Interlocked.Increment(ref _tickLastCalls);
        return _book.Tick(symbol);
    }

    public RawAccount? GetUserAccount(ulong login)
    {
        Interlocked.Increment(ref _getUserAccountCalls);
        return _book.Account(login);
    }

    public List<RawDeal> RequestDeals(ulong login, DateTimeOffset from, DateTimeOffset to)
    {
        Interlocked.Increment(ref _requestDealsCalls);
        return _book.Deals(login, from.ToUnixTimeMilliseconds(), to.ToUnixTimeMilliseconds());
    }

    public void Dispose()
    {
        if (_disposed) return;
        Disconnect();
        _disposed = true;
        _sequences?.Dispose();
    }

    public IReadOnlyDictionary<string, object?> Diagnostics()
    {
        var applied = new Dictionary<string, long>(StringComparer.Ordinal);
        var skipped = new Dictionary<string, long>(StringComparer.Ordinal);
        for (var i = 0; i < FeedStreams.All.Count; i++)
        {
            applied[FeedStreams.All[i]] = Interlocked.Read(ref _applied[i]);
            skipped[FeedStreams.All[i]] = Interlocked.Read(ref _skipped[i]);
        }
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["provider"] = MT5ApiProviders.LiveBridge,
            ["url"] = _options.Url,
            ["state"] = _state,
            ["live"] = IsConnected,
            ["mode"] = _mode,
            ["source"] = _source,
            ["sourceConnected"] = SourceConnected,
            ["filter"] = _filter,
            ["handshakes"] = Interlocked.Read(ref _handshakes),
            ["reconnects"] = Interlocked.Read(ref _reconnects),
            ["byes"] = Interlocked.Read(ref _byes),
            ["lastBye"] = _lastBye,
            ["lastDrop"] = _lastDrop,
            ["framesReceived"] = Interlocked.Read(ref _framesReceived),
            ["unknownFrames"] = Interlocked.Read(ref _unknownFrames),
            ["unmappableRecords"] = Interlocked.Read(ref _unmappable),
            ["lastFrameAtUtc"] = LastFrameAtUtc,
            ["lastHeartbeatAtUtc"] = Ticks(Interlocked.Read(ref _lastHeartbeatTicks)),
            ["applied"] = applied,
            ["skippedBySequence"] = skipped,
            ["gaps"] = new Dictionary<string, long>(StringComparer.Ordinal) { ["ticks"] = Interlocked.Read(ref _tickGaps), ["deals"] = Interlocked.Read(ref _dealGaps) },
            ["sequences"] = _sequences?.Snapshot(),
            ["statePath"] = _sequences?.Path,
            ["stateSavedAtUtc"] = _sequences?.LastSavedUtc,
            ["book"] = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["positions"] = _book.PositionCount,
                ["accounts"] = _book.AccountCount,
                ["symbols"] = _book.SymbolCount,
                ["deals"] = _book.DealCount,
            },
        };
    }

    // ---- The session loop ------------------------------------------------------------------

    private async Task RunAsync(Uri uri, TaskCompletionSource<string?> first, CancellationToken ct)
    {
        var backoff = Math.Max(100, _options.ReconnectMs);
        var isFirst = true;
        while (!ct.IsCancellationRequested && !_closing)
        {
            FeedSocket? socket = null;
            string reason;
            var wasLive = false;
            try
            {
                _state = isFirst ? "connecting" : "reconnecting";
                socket = await FeedSocket.ConnectAsync(uri, _options.ApiKey, _options.CertificateThumbprint,
                    TimeSpan.FromMilliseconds(Math.Max(1000, _options.HandshakeTimeoutMs)), ct).ConfigureAwait(false);
                lock (_gate) _socket = socket;

                await socket.SendTextAsync(FeedWire.Subscribe(_sequences!.Resume()), ct).ConfigureAwait(false);
                _state = "handshake";
                await HandshakeAsync(socket, ct).ConfigureAwait(false);

                _live = true;
                wasLive = true;
                _state = "live";
                backoff = Math.Max(100, _options.ReconnectMs);
                if (isFirst)
                {
                    isFirst = false;
                    first.TrySetResult(null);
                }
                reason = await LiveAsync(socket, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                reason = "disconnect requested";
            }
            catch (Exception ex)
            {
                reason = Describe(ex);
                if (isFirst)
                {
                    LastError = "LiveBridgeApi: " + reason;
                    _logger.LogError("Live Bridge: the first connection to {Url} failed: {Reason}", uri, reason);
                }
            }

            _live = false;
            _lastDrop = reason;
            lock (_gate) _socket = null;
            if (socket is not null)
            {
                await socket.CompleteCloseAsync(reason).ConfigureAwait(false);
                socket.Dispose();
            }
            _sequences?.Flush();

            if (isFirst)
            {
                first.TrySetResult(LastError.Length > 0 ? LastError : "LiveBridgeApi: " + reason);
                break;
            }
            if (ct.IsCancellationRequested || _closing) break;

            Interlocked.Increment(ref _reconnects);
            _state = "reconnecting";
            if (wasLive)
                _logger.LogWarning("Live Bridge: session ended ({Reason}); reconnecting with resume in {Backoff} ms", reason, backoff);
            else
                _logger.LogWarning("Live Bridge: reconnect failed ({Reason}); next attempt in {Backoff} ms", reason, backoff);
            try { await Task.Delay(backoff, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            backoff = Math.Min(backoff * 2, Math.Max(backoff, _options.ReconnectMaxMs));
        }
        _live = false;
        _state = "disconnected";
    }

    /// <summary>hello, then snapshot / replay / replay_gap frames per stream, then snapshot_end or replay_end.</summary>
    private async Task HandshakeAsync(FeedSocket socket, CancellationToken ct)
    {
        using var timed = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timed.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1000, _options.HandshakeTimeoutMs)));
        var sawHello = false;
        var before = Applied();
        _positionSnapshot = false;
        while (true)
        {
            string? text;
            try
            {
                text = await socket.ReceiveTextAsync(TimeSpan.Zero, timed.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timed.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                throw new TimeoutException($"the handshake did not complete within {_options.HandshakeTimeoutMs} ms");
            }
            if (text is null) throw new FeedProtocolException("the bridge closed during the handshake");
            var frame = FeedWire.Parse(text);
            NoteFrame();
            switch (frame)
            {
                case HelloFrame hello:
                    sawHello = true;
                    OnHello(hello);
                    break;
                case ByeFrame bye:
                    Interlocked.Increment(ref _byes);
                    _lastBye = bye.Reason;
                    throw new FeedProtocolException("bye during the handshake: " + bye.Reason);
                case EndFrame end:
                    if (!sawHello) throw new FeedProtocolException($"{end.Type} before hello");
                    OnEnd(end);
                    Interlocked.Increment(ref _handshakes);
                    var after = Applied();
                    _logger.LogInformation(
                        "Live Bridge: {Mode} complete from {Source}: positions={Positions} deals={Deals} accounts={Accounts} ticks={Ticks} applied; resume at {Seq}",
                        _mode, _source, after[0] - before[0], after[1] - before[1], after[2] - before[2], after[3] - before[3],
                        string.Join(", ", end.Seq.Select(kv => $"{kv.Key}={kv.Value}")));
                    return;
                default:
                    ApplyFrame(frame);
                    break;
            }
        }
    }

    /// <summary>Live frames until the bridge says bye, closes, goes silent, or the consumer disconnects. Returns the reason.</summary>
    private async Task<string> LiveAsync(FeedSocket socket, CancellationToken ct)
    {
        using var ackCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var acks = _options.AckEveryMs > 0 ? AckLoopAsync(socket, ackCts.Token) : Task.CompletedTask;
        try
        {
            var silence = TimeSpan.FromMilliseconds(Math.Max(1000, _options.SilenceLimitMs));
            while (!ct.IsCancellationRequested)
            {
                var text = await socket.ReceiveTextAsync(silence, ct).ConfigureAwait(false);
                if (text is null) return _closing ? "consumer disconnecting" : "closed by the bridge";
                var frame = FeedWire.Parse(text);
                NoteFrame();
                if (frame is ByeFrame bye)
                {
                    Interlocked.Increment(ref _byes);
                    _lastBye = bye.Reason;
                    return "bye: " + bye.Reason;
                }
                ApplyFrame(frame);
                PruneIfDue();
            }
            return "disconnect requested";
        }
        catch (TimeoutException ex)
        {
            return ex.Message + " (silent line)";
        }
        finally
        {
            ackCts.Cancel();
            try { await acks.ConfigureAwait(false); } catch (Exception) { }
        }
    }

    private async Task AckLoopAsync(FeedSocket socket, CancellationToken ct)
    {
        var every = TimeSpan.FromMilliseconds(_options.AckEveryMs);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(every, ct).ConfigureAwait(false);
                if (_sequences is null || socket.State != WebSocketState.Open) continue;
                await socket.SendTextAsync(FeedWire.Ack(_sequences.Snapshot()), ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Live Bridge: ack not sent");
        }
    }

    // ---- Applying frames --------------------------------------------------------------------

    private void ApplyFrame(FeedFrame frame)
    {
        switch (frame)
        {
            case BatchFrame batch when batch.Kind == "snapshot":
                if (batch.Stream == FeedStreams.Positions && !_positionSnapshot)
                {
                    _positionSnapshot = true;
                    _book.BeginPositionSnapshot();
                }
                foreach (var r in batch.Records) ApplyRecord(r, snapshot: true);
                break;
            case BatchFrame batch:   // replay, or live records (ticks bundled)
                foreach (var r in batch.Records) ApplyRecord(r, snapshot: false);
                break;
            case RecordFrame record:
                ApplyRecord(record.Record, snapshot: false);
                break;
            case GapFrame gap:
                OnGap(gap);
                break;
            case HeartbeatFrame heartbeat:
                OnHeartbeat(heartbeat);
                break;
            case EndFrame end:
                OnEnd(end);
                break;
            case HelloFrame hello:
                OnHello(hello);
                break;
            case UnknownFrame:
                Interlocked.Increment(ref _unknownFrames);
                break;
        }
    }

    private void ApplyRecord(FeedRecord r, bool snapshot)
    {
        var index = FeedStreams.IndexOf(r.Stream);
        if (index < 0) return;
        if (!snapshot && _sequences is not null && r.Seq <= _sequences.Get(r.Stream))
        {
            Interlocked.Increment(ref _skipped[index]);
            return;
        }

        var applied = false;
        switch (r.Stream)
        {
            case FeedStreams.Positions:
            {
                var position = FeedMapping.Position(r.Payload);
                if (position is null) break;
                var result = _book.ApplyPosition(r.Action, position);
                applied = true;
                if (r.Action == FeedActions.Delete)
                {
                    if (_positionsOn) Raise(OnPositionDelete, position, nameof(OnPositionDelete));
                }
                else if (result == FeedApply.Added)
                {
                    if (_positionsOn) Raise(OnPositionAdd, position, nameof(OnPositionAdd));
                }
                else if (result == FeedApply.Updated)
                {
                    if (_positionsOn) Raise(OnPositionUpdate, position, nameof(OnPositionUpdate));
                }
                break;
            }
            case FeedStreams.Deals:
            {
                var deal = FeedMapping.Deal(r.Payload);
                if (deal is null) break;
                var result = _book.ApplyDeal(deal);
                applied = true;
                if (result != FeedApply.Unchanged && _dealsOn) Raise(OnDealAdd, deal, nameof(OnDealAdd));
                break;
            }
            case FeedStreams.Accounts:
            {
                var account = FeedMapping.Account(r.Payload);
                if (account is null) break;
                var result = _book.ApplyAccount(account);
                applied = true;
                if (result != FeedApply.Unchanged && _usersOn) Raise(OnUserUpdate, account, nameof(OnUserUpdate));
                break;
            }
            case FeedStreams.Ticks:
            {
                var tick = FeedMapping.Tick(r.Payload);
                if (tick is null) break;
                _book.ApplyTick(tick);
                applied = true;
                if (_ticksOn) Raise(OnTick, tick, nameof(OnTick));
                break;
            }
        }

        if (applied) Interlocked.Increment(ref _applied[index]);
        else Interlocked.Increment(ref _unmappable);
        if (!snapshot && r.Seq > 0) _sequences?.Advance(r.Stream, r.Seq);
    }

    private void OnHello(HelloFrame hello)
    {
        _source = hello.Source;
        _mode = hello.Mode;
        _filter = hello.Filter;
        _logger.LogInformation("Live Bridge: hello from {Source} at {ServerTime}, mode {Mode}, filter {Filter}",
            hello.Source, hello.ServerTime, hello.Mode, hello.Filter);
    }

    private void OnEnd(EndFrame end)
    {
        foreach (var (stream, seq) in end.Seq) _sequences?.Advance(stream, seq);
        if (_positionSnapshot)
        {
            _positionSnapshot = false;
            var gone = _book.EndPositionSnapshot();
            if (gone.Count > 0)
            {
                _logger.LogInformation("Live Bridge: {Count} position(s) missing from the snapshot are closed", gone.Count);
                foreach (var p in gone)
                    if (_positionsOn) Raise(OnPositionDelete, p, nameof(OnPositionDelete));
            }
        }
        _sequences?.Flush();
    }

    private void OnGap(GapFrame gap)
    {
        if (gap.Stream == FeedStreams.Ticks)
        {
            Interlocked.Increment(ref _tickGaps);
            _logger.LogInformation("Live Bridge: tick replay gap ({Reason}); taking the last prices that follow", gap.Reason);
            return;
        }
        Interlocked.Increment(ref _dealGaps);
        _logger.LogWarning("Live Bridge: {Stream} could not be replayed exactly ({Reason}); records after sequence {Seq} are not recoverable from the feed",
            gap.Stream, gap.Reason, _sequences?.Get(gap.Stream));
    }

    private void OnHeartbeat(HeartbeatFrame heartbeat)
    {
        Interlocked.Exchange(ref _lastHeartbeatTicks, DateTime.UtcNow.Ticks);
        if (heartbeat.Source is null) return;
        var connected = heartbeat.Source == "connected" ? 1 : 0;
        var before = Interlocked.Exchange(ref _sourceConnected, connected);
        if (before != connected)
        {
            if (connected == 1) _logger.LogInformation("Live Bridge: the bridge reports its MT5 source connected");
            else _logger.LogWarning("Live Bridge: the bridge reports its MT5 source DISCONNECTED; the book is frozen until it returns");
        }
    }

    private void PruneIfDue()
    {
        var now = DateTime.UtcNow.Ticks;
        var last = Interlocked.Read(ref _lastPruneTicks);
        if (now - last < TimeSpan.FromMinutes(30).Ticks) return;
        Interlocked.Exchange(ref _lastPruneTicks, now);
        var keepMs = Math.Max(1, _options.DealRetentionHours) * 3_600_000L;
        var pruned = _book.PruneDeals(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - keepMs);
        if (pruned > 0) _logger.LogDebug("Live Bridge: forgot {Count} deals older than {Hours} h", pruned, _options.DealRetentionHours);
    }

    private void NoteFrame()
    {
        Interlocked.Increment(ref _framesReceived);
        Interlocked.Exchange(ref _lastFrameTicks, DateTime.UtcNow.Ticks);
    }

    private long[] Applied()
    {
        var copy = new long[_applied.Length];
        for (var i = 0; i < copy.Length; i++) copy[i] = Interlocked.Read(ref _applied[i]);
        return copy;
    }

    private void Raise<T>(Action<T>? handler, T argument, string name)
    {
        if (handler is null) return;
        try { handler(argument); }
        catch (Exception ex) { _logger.LogError(ex, "Live Bridge: a {Event} handler failed", name); }
    }

    private static DateTime? Ticks(long ticks) => ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);

    private static string Describe(Exception ex) => ex switch
    {
        FeedConnectException { Status: HttpStatusCode.Unauthorized } => "the feed refused the key (HTTP 401): check LiveBridge__ApiKey against the bridge console",
        FeedConnectException { Status: HttpStatusCode.Forbidden } => "the feed refused this address (HTTP 403): it is not on the consumer's allowlist",
        FeedConnectException { Status: HttpStatusCode.NotFound } => "the feed does not serve the source named in LiveBridge:Url (HTTP 404)",
        FeedConnectException c when c.Status is { } s => $"the feed refused the connection (HTTP {(int)s} {s})",
        FeedConnectException c => "connect failed: " + c.Message,
        TimeoutException t => t.Message,
        FeedProtocolException p => "protocol: " + p.Message,
        WebSocketException w => $"socket: {w.WebSocketErrorCode} {w.Message}",
        _ => ex.GetType().Name + ": " + ex.Message,
    };

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LiveBridgeApi));
    }
}
