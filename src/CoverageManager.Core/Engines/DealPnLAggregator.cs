using CoverageManager.Core.Models;

namespace CoverageManager.Core.Engines;

/// <summary>
/// The realized-P&amp;L rules, in one place. <see cref="DealStore"/> uses them for its in-memory
/// working set and the history reader uses them for Postgres-backed ranges, so both surfaces
/// always agree:
///   * trade deals only (action 0/1) with a symbol -- balance/credit/correction rows are bookkeeping;
///   * volume from ALL trade deals (IN + OUT), matching MT5 Manager totals;
///   * profit and swap from OUT deals only (entry 1, 2, 3) -- only closes carry them;
///   * commission and fee from every trade deal.
/// </summary>
public static class DealPnLAggregator
{
    private static bool IsTrade(ClosedDeal d) => d.Action < 2 && !string.IsNullOrEmpty(d.Symbol);
    private static bool IsOut(ClosedDeal d) => d.Entry == 1 || d.Entry == 2 || d.Entry == 3;

    /// <summary>Per-symbol summary, largest |net| first.</summary>
    public static IReadOnlyList<SymbolPnL> BySymbol(IEnumerable<ClosedDeal> deals) =>
        deals.Where(IsTrade)
             .GroupBy(d => d.Symbol)
             .Select(g => Summarize(g.Key, g))
             .OrderByDescending(p => Math.Abs(p.NetPnL))
             .ToList()
             .AsReadOnly();

    /// <summary>Per-day summary (by the deal time's date), newest day first, each with its per-symbol rows.</summary>
    public static IReadOnlyList<DailyPnL> ByDay(IEnumerable<ClosedDeal> deals) =>
        deals.Where(IsTrade)
             .GroupBy(d => d.Time.Date)
             .OrderByDescending(g => g.Key)
             .Select(day => new DailyPnL
             {
                 Date = day.Key,
                 DealCount = day.Count(),
                 TotalProfit = day.Where(IsOut).Sum(d => d.Profit),
                 TotalCommission = day.Sum(d => d.Commission),
                 TotalSwap = day.Where(IsOut).Sum(d => d.Swap),
                 TotalFee = day.Sum(d => d.Fee),
                 Symbols = day.GroupBy(d => d.Symbol)
                              .Select(sg => Summarize(sg.Key, sg))
                              .OrderByDescending(p => Math.Abs(p.NetPnL))
                              .ToList(),
             })
             .ToList()
             .AsReadOnly();

    private static SymbolPnL Summarize(string symbol, IEnumerable<ClosedDeal> group)
    {
        var all = group as IList<ClosedDeal> ?? group.ToList();
        var outs = all.Where(IsOut).ToList();
        return new SymbolPnL
        {
            Symbol = symbol,
            DealCount = all.Count,
            TotalProfit = outs.Sum(d => d.Profit),
            TotalCommission = all.Sum(d => d.Commission),
            TotalSwap = outs.Sum(d => d.Swap),
            TotalFee = all.Sum(d => d.Fee),
            TotalVolume = all.Sum(d => d.VolumeLots),
            BuyVolume = all.Where(d => d.Direction == "BUY").Sum(d => d.VolumeLots),
            SellVolume = all.Where(d => d.Direction == "SELL").Sum(d => d.VolumeLots),
        };
    }
}
