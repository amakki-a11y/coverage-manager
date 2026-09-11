namespace CoverageManager.Connector;

/// <summary>
/// Creates the <see cref="IMT5Api"/> implementation the connector services talk to.
/// Resolved once at startup from config (<c>MT5:Provider</c>) - see <see cref="MT5ApiFactory"/>.
/// Injected into <see cref="MT5ManagerConnection"/> / <see cref="MT5CoverageConnection"/> so the
/// B-Book feed can be switched from the MetaQuotes Manager API to the Live Bridge push feed
/// without touching the connection logic (bring-up order, event wiring, reconnect backoff).
/// </summary>
public interface IMT5ApiFactory
{
    /// <summary>
    /// Canonical provider name: <see cref="MT5ApiProviders.Manager"/> or
    /// <see cref="MT5ApiProviders.LiveBridge"/>. Surfaced on /api/exposure/status.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// True when the provider connects with MT5 manager credentials from <c>account_settings</c> (the Manager API).
    /// False when it is keyed by its own configuration (the Live Bridge feed: URL + bearer key), so the bring-up must
    /// not read - or wait for - <c>account_settings</c> at all.
    /// </summary>
    bool RequiresManagerAccount { get; }

    /// <summary>
    /// Where the provider connects, for logs and the status endpoint: the feed URL for the Live Bridge, null for the
    /// Manager API (whose server comes from the manager account).
    /// </summary>
    string? Endpoint { get; }

    /// <summary>Creates a fresh, not-yet-initialized API instance. The caller owns disposal.</summary>
    IMT5Api Create();
}
