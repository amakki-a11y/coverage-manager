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
/// <para>Pure / synchronous: no IO, no persistent locks beyond the <c>PositionManager</c> read.</para>
/// </summary>
public class ExposureEngine
{
    private readonly PositionManager _positionManager;

    public ExposureEngine(PositionManager positionManager)
    {
        _positionManager = positionManager;
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
            summary.BBookPnL = bbookPositions.Sum(p => p.Profit + p.Swap);

            // Coverage aggregation (already normalized)
            var covBuys = coveragePositions.Where(p => p.Direction == "BUY").ToList();
            var covSells = coveragePositions.Where(p => p.Direction == "SELL").ToList();
            summary.CoverageBuyVolume = covBuys.Sum(p => p.VolumeNormalized);
            summary.CoverageSellVolume = covSells.Sum(p => p.VolumeNormalized);
            summary.CoverageBuyAvgPrice = WeightedAvgPrice(covBuys);
            summary.CoverageSellAvgPrice = WeightedAvgPrice(covSells);
            summary.CoveragePnL = coveragePositions.Sum(p => p.Profit + p.Swap);

            summaries.Add(summary);
        }

        return summaries.OrderByDescending(s => Math.Abs(s.NetVolume)).ToList();
    }

    private static decimal WeightedAvgPrice(List<Position> positions)
    {
        var totalVolume = positions.Sum(p => p.VolumeNormalized);
        if (totalVolume == 0) return 0;
        return positions.Sum(p => p.OpenPrice * p.VolumeNormalized) / totalVolume;
    }
}
