using System.Collections.Concurrent;

namespace CoverageManager.Core.Engines;

/// <summary>
/// Resolves pip size for a given symbol. Used to convert raw price edge into pips.
/// Pluggable — can be replaced per broker via <see cref="Overrides"/>.
/// </summary>
public static class BridgePipResolver
{
    /// <summary>
    /// Per-symbol overrides. Takes precedence over the heuristic.
    /// Consumer code populates this from symbol_mappings or a config file.
    ///
    /// Thread-safe by construction — <see cref="PositionManager.LoadMappings"/> rewrites
    /// these entries on every admin save while the Bridge worker + REST controllers are
    /// reading. Plain <c>Dictionary&lt;,&gt;</c> would tear or throw on concurrent enumeration.
    /// </summary>
    public static readonly ConcurrentDictionary<string, decimal> Overrides =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Resolve pip size.
    ///   1. Overrides by canonical symbol (exact match, case-insensitive).
    ///   2. Well-known symbols (XAU=0.1, XAG=0.01, JPY pairs=0.01).
    ///   3. Heuristic by price magnitude (matches Markup tab convention).
    /// </summary>
    public static decimal GetPipSize(string symbol, decimal samplePrice)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return HeuristicFromPrice(samplePrice);

        if (Overrides.TryGetValue(symbol, out var explicitPip) && explicitPip > 0m)
            return explicitPip;

        var upper = symbol.ToUpperInvariant();
        if (upper.Contains("XAU")) return 0.1m;
        if (upper.Contains("XAG")) return 0.001m;
        if (upper.EndsWith("JPY")) return 0.01m;

        // Standard 6-char FX pair like EURUSD, GBPCHF, AUDCAD → 5-digit quote, pip = 0.0001
        if (IsStandardFxPair(upper)) return 0.0001m;

        return HeuristicFromPrice(samplePrice);
    }

    private static bool IsStandardFxPair(string upper)
    {
        if (upper.Length != 6) return false;
        for (var i = 0; i < 6; i++)
        {
            if (upper[i] < 'A' || upper[i] > 'Z') return false;
        }
        return true;
    }

    private static decimal HeuristicFromPrice(decimal price)
    {
        var abs = Math.Abs(price);
        if (abs >= 1000m) return 0.1m;    // Indices, metals quoted in thousands
        if (abs >= 10m)   return 0.01m;   // Oil, JPY-like levels
        if (abs >= 0.1m)  return 0.0001m; // FX range (1.10000, 0.65400)
        return 0.00001m;                  // Small-cap crypto
    }
}
