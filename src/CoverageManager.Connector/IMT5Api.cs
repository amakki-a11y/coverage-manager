namespace CoverageManager.Connector;

/// <summary>
/// Abstraction over the MT5 Manager API.
/// Decouples connector logic from native MetaQuotes types so the solution builds
/// without the DLLs on the machine (when MT5_API_AVAILABLE is not defined).
/// </summary>
public interface IMT5Api : IDisposable
{
    bool Initialize();
    bool Connect(string server, ulong login, string password, uint timeoutMs = 30000);
    void Disconnect();
    bool IsConnected { get; }
    string LastError { get; }

    // Tick subscriptions
    event Action<RawTick>? OnTick;
    bool SubscribeTicks(string symbolMask = "*");
    void UnsubscribeTicks();
    bool SelectedAddAll();

    // Deal subscriptions
    event Action<RawDeal>? OnDealAdd;
    bool SubscribeDeals();
    void UnsubscribeDeals();
    List<RawDeal> RequestDeals(ulong login, DateTimeOffset from, DateTimeOffset to);

    // Position queries
    List<RawPosition> GetPositions(ulong login);
    ulong[] GetUserLogins(string groupMask);

    // Price queries
    RawTick? GetTickLast(string symbol);
}
