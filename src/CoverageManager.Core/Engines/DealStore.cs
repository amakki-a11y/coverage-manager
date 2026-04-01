using System.Collections.Concurrent;
using CoverageManager.Core.Models;

namespace CoverageManager.Core.Engines;

/// <summary>
/// In-memory store of closed deals. Tracks realized P&L per symbol.
/// Thread-safe for concurrent deal callbacks and API reads.
/// </summary>
public class DealStore
{
    private readonly ConcurrentDictionary<ulong, ClosedDeal> _deals = new();

    /// <summary>
    /// Add a deal (deduplicated by DealId).
    /// </summary>
    public void AddDeal(ClosedDeal deal)
    {
        _deals[deal.DealId] = deal;
    }

    /// <summary>
    /// Add multiple deals (e.g., from historical backfill).
    /// </summary>
    public void AddDeals(IEnumerable<ClosedDeal> deals)
    {
        foreach (var d in deals)
            _deals[d.DealId] = d;
    }

    /// <summary>
    /// Clear all deals (e.g., on reconnect for fresh backfill).
    /// </summary>
    public void Clear() => _deals.Clear();

    /// <summary>
    /// Get all stored deals.
    /// </summary>
    public IReadOnlyList<ClosedDeal> GetAllDeals() =>
        _deals.Values.ToList().AsReadOnly();

    /// <summary>
    /// Get realized P&L summary grouped by symbol.
    /// Only includes deals with Entry=OUT (1) — actual closes that carry P&L.
    /// </summary>
    public IReadOnlyList<SymbolPnL> GetPnLBySymbol()
    {
        return _deals.Values
            .Where(d => d.Entry == 1 || d.Entry == 2 || d.Entry == 3) // OUT, INOUT, OUT_BY
            .Where(d => !string.IsNullOrEmpty(d.Symbol))
            .GroupBy(d => d.Symbol)
            .Select(g => new SymbolPnL
            {
                Symbol = g.Key,
                DealCount = g.Count(),
                TotalProfit = g.Sum(d => d.Profit),
                TotalCommission = g.Sum(d => d.Commission),
                TotalSwap = g.Sum(d => d.Swap),
                TotalFee = g.Sum(d => d.Fee),
                TotalVolume = g.Sum(d => d.VolumeLots),
                BuyVolume = g.Where(d => d.Direction == "BUY").Sum(d => d.VolumeLots),
                SellVolume = g.Where(d => d.Direction == "SELL").Sum(d => d.VolumeLots)
            })
            .OrderByDescending(p => Math.Abs(p.NetPnL))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Get realized P&L grouped by date then symbol.
    /// </summary>
    public IReadOnlyList<DailyPnL> GetPnLByDay()
    {
        return _deals.Values
            .Where(d => d.Entry == 1 || d.Entry == 2 || d.Entry == 3)
            .Where(d => !string.IsNullOrEmpty(d.Symbol))
            .GroupBy(d => d.Time.Date)
            .OrderByDescending(g => g.Key)
            .Select(dayGroup => new DailyPnL
            {
                Date = dayGroup.Key,
                DealCount = dayGroup.Count(),
                TotalProfit = dayGroup.Sum(d => d.Profit),
                TotalCommission = dayGroup.Sum(d => d.Commission),
                TotalSwap = dayGroup.Sum(d => d.Swap),
                TotalFee = dayGroup.Sum(d => d.Fee),
                Symbols = dayGroup
                    .GroupBy(d => d.Symbol)
                    .Select(sg => new SymbolPnL
                    {
                        Symbol = sg.Key,
                        DealCount = sg.Count(),
                        TotalProfit = sg.Sum(d => d.Profit),
                        TotalCommission = sg.Sum(d => d.Commission),
                        TotalSwap = sg.Sum(d => d.Swap),
                        TotalFee = sg.Sum(d => d.Fee),
                        TotalVolume = sg.Sum(d => d.VolumeLots),
                        BuyVolume = sg.Where(d => d.Direction == "BUY").Sum(d => d.VolumeLots),
                        SellVolume = sg.Where(d => d.Direction == "SELL").Sum(d => d.VolumeLots)
                    })
                    .OrderByDescending(p => Math.Abs(p.NetPnL))
                    .ToList()
            })
            .ToList()
            .AsReadOnly();
    }

    public int DealCount => _deals.Count;
}
