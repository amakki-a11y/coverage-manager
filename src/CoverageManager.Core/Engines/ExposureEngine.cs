using CoverageManager.Core.Models;

namespace CoverageManager.Core.Engines;

/// <summary>
/// Calculates net exposure by combining B-Book and coverage positions per canonical symbol.
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
