using CoverageManager.Core.Models;

namespace CoverageManager.Core.Engines;

/// <summary>
/// Combines B-Book and coverage positions held in <see cref="PositionManager"/>
/// into a per-canonical-symbol <see cref="ExposureSummary"/> used by the live
/// Exposure view, the WebSocket broadcast, and every floating-P&amp;L surface
/// in the app (Compare, Net P&amp;L's "Current" column, Equity P&amp;L diagnostics).
///
/// <para>Key outputs per symbol:</para>
/// <list type="bullet">
///   <item><c>BBook*</c> / <c>Coverage*</c> volumes and VWAP — raw aggregation by side.</item>
///   <item><c>BBookPnL</c> / <c>CoveragePnL</c> — sum of <c>Profit + Swap</c> across open positions.</item>
///   <item><c>NetVolume = BBookNet − CoverageNet</c> — "To Cover" column.</item>
///   <item><c>NetPnL = −BBookPnL + CoveragePnL</c> — broker's edge on currently-open positions.</item>
///   <item><c>HedgeRatio</c> — <c>|CoverageNet| / |BBookNet|</c>, uncapped (may exceed 100%).</item>
/// </list>
///
/// <para><b>Live floating P&amp;L:</b> when a <see cref="PriceCache"/> is provided
/// the engine recomputes per-position floating from the live tick price
/// using a <em>calibrated delta</em> — anchor on MT5's authoritative
/// <c>Position.Profit</c> and add the USD value of the price move since
/// MT5's last reported price. The "USD per price point" coefficient is
/// derived <em>from the position itself</em>
/// (<c>Profit / (CurrentPrice − OpenPrice)</c>), so it works regardless of
/// whether <c>SymbolMapping.BBookContractSize</c> is correctly populated for
/// the instrument (index/futures CFDs are commonly mis-configured). When
/// the calibration can't be derived (Profit = 0, no price movement yet, or
/// PriceCache miss) we fall back to <c>Position.Profit</c>.</para>
///
/// <para>Pure / synchronous: no IO, no persistent locks beyond the
/// <c>PositionManager</c> read.</para>
/// </summary>
public class ExposureEngine
{
    private readonly PositionManager _positionManager;
    private readonly PriceCache? _priceCache;

    /// <summary>
    /// Production constructor — pass the shared <see cref="PriceCache"/> so
    /// floating P&amp;L can be recomputed from live ticks. The single-arg
    /// overload is kept for tests that don't need a live price feed.
    /// </summary>
    public ExposureEngine(PositionManager positionManager, PriceCache? priceCache = null)
    {
        _positionManager = positionManager;
        _priceCache = priceCache;
    }

    public List<ExposureSummary> CalculateExposure()
    {
        var positions = _positionManager.GetAllPositions();
        var grouped = positions.GroupBy(p => p.CanonicalSymbol);
        var summaries = new List<ExposureSummary>();

        foreach (var group in grouped)
        {
            var summary = new ExposureSummary
            {
                CanonicalSymbol = group.Key,
                UpdatedAt = DateTime.UtcNow
            };

            var bbookPositions = group.Where(p => p.Source == "bbook").ToList();
            var coveragePositions = group.Where(p => p.Source == "coverage").ToList();

            // B-Book aggregation
            var bbBuys = bbookPositions.Where(p => p.Direction == "BUY").ToList();
            var bbSells = bbookPositions.Where(p => p.Direction == "SELL").ToList();
            summary.BBookBuyVolume = bbBuys.Sum(p => p.VolumeNormalized);
            summary.BBookSellVolume = bbSells.Sum(p => p.VolumeNormalized);
            summary.BBookBuyAvgPrice = WeightedAvgPrice(bbBuys);
            summary.BBookSellAvgPrice = WeightedAvgPrice(bbSells);
            summary.BBookPnL = bbookPositions.Sum(p => LivePnL(p) + p.Swap);

            // Coverage aggregation (already normalized)
            var covBuys = coveragePositions.Where(p => p.Direction == "BUY").ToList();
            var covSells = coveragePositions.Where(p => p.Direction == "SELL").ToList();
            summary.CoverageBuyVolume = covBuys.Sum(p => p.VolumeNormalized);
            summary.CoverageSellVolume = covSells.Sum(p => p.VolumeNormalized);
            summary.CoverageBuyAvgPrice = WeightedAvgPrice(covBuys);
            summary.CoverageSellAvgPrice = WeightedAvgPrice(covSells);
            summary.CoveragePnL = coveragePositions.Sum(p => LivePnL(p) + p.Swap);

            summaries.Add(summary);
        }

        return summaries.OrderByDescending(s => Math.Abs(s.NetVolume)).ToList();
    }

    /// <summary>
    /// Per-position floating P&amp;L with live-tick calibration. Returns
    /// <c>Position.Profit</c> (MT5's authoritative value) when the live
    /// calibration can't be derived; otherwise returns
    /// <c>Profit + (livePrice − cachedPrice) × usdPerPoint</c>, where
    /// <c>usdPerPoint</c> is inferred from MT5's own numbers
    /// (<c>Profit / priceDelta-since-open</c>) so the formula stays correct
    /// even when <c>SymbolMapping.BBookContractSize</c> is mis-configured
    /// for the instrument.
    /// </summary>
    internal decimal LivePnL(Position p)
    {
        if (_priceCache is null) return p.Profit;

        var quote = _priceCache.Get(p.Symbol);
        if (quote is null || quote.Bid <= 0 || quote.Ask <= 0) return p.Profit;

        // BUY closes at the current bid (we'd hit the bid to flatten);
        // SELL closes at the current ask.
        var livePrice  = p.Direction == "BUY" ? quote.Bid : quote.Ask;
        var openDelta  = p.CurrentPrice - p.OpenPrice;

        // No movement since open OR no MT5 profit reported yet → we have no
        // anchor to calibrate against. Fall back to MT5's authoritative value
        // so we don't display garbage on freshly-opened positions.
        if (openDelta == 0m || p.Profit == 0m) return p.Profit;

        var usdPerPoint = p.Profit / openDelta;          // sign already baked in
        var newDelta    = livePrice - p.OpenPrice;
        return newDelta * usdPerPoint;
    }

    private static decimal WeightedAvgPrice(List<Position> positions)
    {
        var totalVolume = positions.Sum(p => p.VolumeNormalized);
        if (totalVolume == 0) return 0;
        return positions.Sum(p => p.OpenPrice * p.VolumeNormalized) / totalVolume;
    }

    /// <summary>
    /// "Skinny" floating P&amp;L computation used by the price-only fast path
    /// (Phase 2.17). Walks all positions once, computes <see cref="LivePnL"/>
    /// per position, and returns the per-canonical-symbol B-Book / Coverage
    /// sums. Includes <c>Swap</c> so it matches what
    /// <see cref="CalculateExposure"/> puts in <c>ExposureSummary.BBookPnL</c>
    /// / <c>CoveragePnL</c>.
    ///
    /// <para>~10× cheaper than <see cref="CalculateExposure"/> on a busy book —
    /// no canonical grouping, no weighted-avg-price recomputation, no hedge
    /// ratio. Lets <see cref="ExposureBroadcastService.BroadcastPricesIfDirty"/>
    /// push fresh floating P&amp;L on every tick (~20 Hz) without paying the
    /// full aggregation cost the heavy <c>exposure_update</c> frame already
    /// pays at ~7 Hz.</para>
    /// </summary>
    public IReadOnlyList<FloatingPnL> GetFloatingPnLPerSymbol()
    {
        var positions = _positionManager.GetAllPositions();
        // Two dictionaries instead of grouping — cheaper and produces the
        // exact per-side split the frontend overlays onto ExposureSummary.
        var bbook    = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var coverage = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in positions)
        {
            var contribution = LivePnL(p) + p.Swap;
            var bucket = p.Source == "bbook" ? bbook : coverage;
            bucket[p.CanonicalSymbol] = (bucket.TryGetValue(p.CanonicalSymbol, out var sum) ? sum : 0m)
                                       + contribution;
        }

        // Union the keys so a symbol with only one side (e.g. coverage but no
        // open clients yet) still gets a row with the other side defaulted to 0.
        var symbols = new HashSet<string>(bbook.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var k in coverage.Keys) symbols.Add(k);

        var result = new List<FloatingPnL>(symbols.Count);
        foreach (var sym in symbols)
        {
            result.Add(new FloatingPnL
            {
                CanonicalSymbol = sym,
                BBook    = bbook.TryGetValue(sym,    out var b) ? b : 0m,
                Coverage = coverage.TryGetValue(sym, out var c) ? c : 0m,
            });
        }
        return result;
    }
}

/// <summary>
/// Per-canonical-symbol floating P&amp;L decomposition shipped on the
/// <c>price_update</c> WebSocket frame (Phase 2.17). Frontend
/// <c>useExposureSocket</c> reducer overlays <c>BBook</c>/<c>Coverage</c>
/// onto <c>ExposureSummary.bBookPnL</c>/<c>coveragePnL</c> and recomputes
/// <c>netPnL = −bBook + Coverage</c> so the Exposure open row, Net P&amp;L
/// tab "Current Floating", and Topbar tile all tick at full price cadence.
/// </summary>
public class FloatingPnL
{
    public string CanonicalSymbol { get; set; } = string.Empty;
    public decimal BBook { get; set; }
    public decimal Coverage { get; set; }
}
