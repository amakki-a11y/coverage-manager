namespace CoverageManager.Connector;

/// <summary>
/// Settings for the Live Bridge push feed, bound from the <c>LiveBridge</c> config section.
/// Only consulted when <c>MT5:Provider</c> is <see cref="MT5ApiProviders.LiveBridge"/>.
/// </summary>
public sealed class LiveBridgeOptions
{
    public const string SectionName = "LiveBridge";

    /// <summary>
    /// Push-feed endpoint (expected ws:// or wss://). Empty until the Live Bridge publishes
    /// its feed; <see cref="LiveBridgeApi"/> reports it in <see cref="IMT5Api.LastError"/>.
    /// </summary>
    public string Url { get; set; } = "";

    /// <summary>
    /// Feed credential. Must come from the environment (<c>LiveBridge__ApiKey</c>), never
    /// from appsettings.json - that file is tracked in git.
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Reconnect delay the future implementation will use between feed drops.</summary>
    public int ReconnectMs { get; set; } = 5000;
}
