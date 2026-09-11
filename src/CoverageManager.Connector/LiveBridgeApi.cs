using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoverageManager.Connector;

/// <summary>
/// Placeholder <see cref="IMT5Api"/> for the upcoming Live Bridge push feed.
///
/// Selected with <c>MT5:Provider = "LiveBridge"</c> (see <see cref="MT5ApiFactory"/>). Until the
/// Live Bridge publishes its feed contract this class is intentionally EMPTY: every member is a
/// safe no-op. <see cref="Connect"/> always fails with a descriptive <see cref="LastError"/>, so
/// <see cref="MT5ManagerConnection"/> keeps retrying with its normal 1s-60s backoff and the
/// dashboard shows the MT5 health dot red instead of the process crashing.
///
/// When the feed is published, implement the members below; nothing outside this class needs
/// to change because the connection services only talk to <see cref="IMT5Api"/>:
///   * Initialize / Connect / Disconnect  - open and close the push-feed session
///                                          (<see cref="LiveBridgeOptions.Url"/> + ApiKey).
///   * OnTick / OnDealAdd / OnPosition* / OnUserUpdate - raise from the feed's push messages,
///                                          mapped onto the Raw* types in RawTypes.cs.
///   * Subscribe* / Unsubscribe*          - feed subscription control messages.
///   * GetPositions / GetUserLogins / GetUserAccount / RequestDeals / GetTickLast
///                                        - snapshot / replay requests (used by the bring-up
///                                          order in MT5ManagerConnection.ExecuteAsync).
/// Keep the call counters: /api/exposure/diagnostics reports them per provider.
/// </summary>
public sealed class LiveBridgeApi : IMT5Api
{
    private readonly LiveBridgeOptions _options;
    private readonly ILogger _logger;
    private bool _disposed;

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

    // Events are declared to satisfy IMT5Api; the placeholder never raises them.
#pragma warning disable CS0067
    public event Action<RawTick>? OnTick;
    public event Action<RawDeal>? OnDealAdd;
    public event Action<RawPosition>? OnPositionAdd;
    public event Action<RawPosition>? OnPositionUpdate;
    public event Action<RawPosition>? OnPositionDelete;
    public event Action<RawAccount>? OnUserUpdate;
#pragma warning restore CS0067

    public bool IsConnected => false;
    public string LastError { get; private set; } = "";

    public long GetPositionsCalls => Interlocked.Read(ref _getPositionsCalls);
    public long GetUserAccountCalls => Interlocked.Read(ref _getUserAccountCalls);
    public long GetUserLoginsCalls => Interlocked.Read(ref _getUserLoginsCalls);
    public long RequestDealsCalls => Interlocked.Read(ref _requestDealsCalls);
    public long TickLastCalls => Interlocked.Read(ref _tickLastCalls);

    // ---- Session -------------------------------------------------------------------------

    public bool Initialize()
    {
        ThrowIfDisposed();
        LastError = "";
        return true;
    }

    /// <summary>
    /// Always fails until the feed exists. The password is deliberately NOT echoed into
    /// <see cref="LastError"/> - the message is logged and shown on the diagnostics endpoint.
    /// </summary>
    public bool Connect(string server, ulong login, string password, uint timeoutMs = 30000)
    {
        ThrowIfDisposed();
        var url = string.IsNullOrWhiteSpace(_options.Url) ? "(LiveBridge:Url not set)" : _options.Url;
        LastError = "LiveBridgeApi: the Live Bridge push feed is not implemented yet " +
                    $"(url={url}, requested server={server}, login={login}). " +
                    "Set MT5:Provider=Manager to use the MetaQuotes Manager API.";
        _logger.LogWarning("{Error}", LastError);
        return false;
    }

    public void Disconnect() { }

    // ---- Subscriptions (all inert) -------------------------------------------------------

    public bool SubscribeTicks(string symbolMask = "*") => NotImplemented(nameof(SubscribeTicks));
    public void UnsubscribeTicks() { }
    public bool SelectedAddAll() => NotImplemented(nameof(SelectedAddAll));

    public bool SubscribeDeals() => NotImplemented(nameof(SubscribeDeals));
    public void UnsubscribeDeals() { }

    public bool SubscribePositions() => NotImplemented(nameof(SubscribePositions));
    public void UnsubscribePositions() { }

    public bool SubscribeUsers() => NotImplemented(nameof(SubscribeUsers));
    public void UnsubscribeUsers() { }

    // ---- Queries (empty results, counted) ------------------------------------------------

    public List<RawDeal> RequestDeals(ulong login, DateTimeOffset from, DateTimeOffset to)
    {
        Interlocked.Increment(ref _requestDealsCalls);
        return new List<RawDeal>();
    }

    public List<RawPosition> GetPositions(ulong login)
    {
        Interlocked.Increment(ref _getPositionsCalls);
        return new List<RawPosition>();
    }

    public ulong[] GetUserLogins(string groupMask)
    {
        Interlocked.Increment(ref _getUserLoginsCalls);
        return Array.Empty<ulong>();
    }

    public RawTick? GetTickLast(string symbol)
    {
        Interlocked.Increment(ref _tickLastCalls);
        return null;
    }

    public RawAccount? GetUserAccount(ulong login)
    {
        Interlocked.Increment(ref _getUserAccountCalls);
        return null;
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private bool NotImplemented(string member)
    {
        LastError = $"LiveBridgeApi.{member}: not implemented yet (Live Bridge feed not published).";
        return false;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LiveBridgeApi));
    }
}
