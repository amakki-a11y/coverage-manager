using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoverageManager.Connector;

/// <summary>Known values for the <c>MT5:Provider</c> config key.</summary>
public static class MT5ApiProviders
{
    /// <summary>MetaQuotes Manager API (native DLLs under Libs\). The default.</summary>
    public const string Manager = "Manager";

    /// <summary>Live Bridge push feed. Placeholder implementation until the feed is published.</summary>
    public const string LiveBridge = "LiveBridge";

    public static readonly IReadOnlyList<string> All = new[] { Manager, LiveBridge };

    /// <summary>
    /// Maps a raw config value to a canonical provider name. Null/blank selects
    /// <see cref="Manager"/>; matching is case-insensitive; anything else throws so a typo in
    /// appsettings.json or the environment fails at startup with a clear message instead of
    /// looping inside the reconnect backoff.
    /// </summary>
    public static string Normalize(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider)) return Manager;

        var trimmed = provider.Trim();
        foreach (var known in All)
        {
            if (string.Equals(known, trimmed, StringComparison.OrdinalIgnoreCase))
                return known;
        }

        throw new ArgumentException(
            $"Unknown MT5:Provider '{provider}'. Valid values: {string.Join(", ", All)}.",
            nameof(provider));
    }
}

/// <summary>
/// Default <see cref="IMT5ApiFactory"/>: hands out <see cref="MT5ApiReal"/> (Manager API) or
/// <see cref="LiveBridgeApi"/> depending on the provider resolved at construction time.
/// </summary>
public sealed class MT5ApiFactory : IMT5ApiFactory
{
    private readonly LiveBridgeOptions _liveBridgeOptions;
    private readonly ILoggerFactory _loggerFactory;

    public string ProviderName { get; }

    /// <inheritdoc />
    public bool RequiresManagerAccount => ProviderName == MT5ApiProviders.Manager;

    /// <inheritdoc />
    public string? Endpoint => ProviderName == MT5ApiProviders.LiveBridge ? _liveBridgeOptions.Url : null;

    /// <param name="provider">Raw <c>MT5:Provider</c> value; null/blank selects the Manager API.</param>
    /// <param name="liveBridgeOptions">Bound <c>LiveBridge</c> section; only used by the LiveBridge provider.</param>
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

    public IMT5Api Create()
    {
        switch (ProviderName)
        {
            case MT5ApiProviders.LiveBridge:
                return new LiveBridgeApi(_liveBridgeOptions, _loggerFactory.CreateLogger<LiveBridgeApi>());

            case MT5ApiProviders.Manager:
#if MT5_API_AVAILABLE
                return new MT5ApiReal();
#else
                throw new NotSupportedException(
                    "MT5:Provider=Manager requires the MetaQuotes Manager API DLLs (build with MT5_API_AVAILABLE).");
#endif

            default:
                throw new InvalidOperationException($"Unhandled MT5 API provider '{ProviderName}'.");
        }
    }
}
