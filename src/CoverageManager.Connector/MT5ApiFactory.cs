using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoverageManager.Connector;

/// <summary>
/// Known values for the <c>MT5:Provider</c> config key.
///
/// <para>v2 (Phase 2, source consolidation): the Live Bridge consumer feed is the ONLY
/// B-Book source. The MetaQuotes Manager API provider and its native DLLs were removed —
/// the bridge owns ingestion, reconnect, gap-fill and multi-server, so the app consumes
/// one authoritative stream. <c>Manager</c> is deliberately rejected with a pointed
/// message rather than silently ignored, so an old config fails fast at startup instead
/// of looking like a feed outage.</para>
/// </summary>
public static class MT5ApiProviders
{
    /// <summary>The Live Bridge push feed — the only supported provider in v2.</summary>
    public const string LiveBridge = "LiveBridge";

    public static readonly IReadOnlyList<string> All = new[] { LiveBridge };

    /// <summary>
    /// Maps a raw config value to a canonical provider name. Null/blank selects
    /// <see cref="LiveBridge"/> (the only provider); matching is case-insensitive; anything
    /// else throws at startup with a clear message.
    /// </summary>
    public static string Normalize(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider)) return LiveBridge;

        var trimmed = provider.Trim();
        foreach (var known in All)
        {
            if (string.Equals(known, trimmed, StringComparison.OrdinalIgnoreCase))
                return known;
        }

        if (string.Equals("Manager", trimmed, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "MT5:Provider=Manager is no longer supported: v2 removed the MetaQuotes Manager API " +
                "connector and its native DLLs. The Live Bridge feed is the only B-Book source — " +
                "set MT5:Provider=LiveBridge (or leave it blank) and configure the LiveBridge section.",
                nameof(provider));
        }

        throw new ArgumentException(
            $"Unknown MT5:Provider '{provider}'. Valid values: {string.Join(", ", All)}.",
            nameof(provider));
    }
}

/// <summary>
/// Default <see cref="IMT5ApiFactory"/>: hands out a <see cref="LiveBridgeApi"/> over the
/// Live Bridge consumer feed. Single-source by construction (see <see cref="MT5ApiProviders"/>).
/// </summary>
public sealed class MT5ApiFactory : IMT5ApiFactory
{
    private readonly LiveBridgeOptions _liveBridgeOptions;
    private readonly ILoggerFactory _loggerFactory;

    public string ProviderName { get; }

    /// <inheritdoc />
    /// <remarks>
    /// Always false in v2: the feed is keyed by its own configuration (URL + bearer key), so the
    /// bring-up never reads — or waits for — <c>account_settings</c>. A store outage therefore
    /// cannot block feed bring-up.
    /// </remarks>
    public bool RequiresManagerAccount => false;

    /// <inheritdoc />
    public bool DialEnabled => _liveBridgeOptions.Enabled;

    /// <inheritdoc />
    public string? Endpoint => _liveBridgeOptions.Url;

    /// <param name="provider">Raw <c>MT5:Provider</c> value; null/blank selects the Live Bridge feed.</param>
    /// <param name="liveBridgeOptions">Bound <c>LiveBridge</c> section.</param>
    /// <param name="loggerFactory">Optional; falls back to <see cref="NullLoggerFactory"/> (tests).</param>
    public MT5ApiFactory(
        string? provider = null,
        LiveBridgeOptions? liveBridgeOptions = null,
        ILoggerFactory? loggerFactory = null)
    {
        ProviderName = MT5ApiProviders.Normalize(provider);
        _liveBridgeOptions = liveBridgeOptions ?? new LiveBridgeOptions();
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    }

    public IMT5Api Create() =>
        new LiveBridgeApi(_liveBridgeOptions, _loggerFactory.CreateLogger<LiveBridgeApi>());
}
