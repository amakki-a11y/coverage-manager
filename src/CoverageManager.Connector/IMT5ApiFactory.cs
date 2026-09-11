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

    /// <summary>Creates a fresh, not-yet-initialized API instance. The caller owns disposal.</summary>
    IMT5Api Create();
}
