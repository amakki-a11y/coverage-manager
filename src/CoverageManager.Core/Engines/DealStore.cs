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
    // Secondary index: (login, orderId) -> dealId. Used by the Bridge to resolve Centroid ext_order -> true MT5 deal number.
    private readonly ConcurrentDictionary<(ulong login, ulong orderId), ulong> _dealIdByOrder = new();

    /// <summary>
    /// Add a deal (deduplicated by DealId).
    /// </summary>
    public void AddDeal(ClosedDeal deal)
    {
        _deals[deal.DealId] = deal;
        if (deal.Login != 0 && deal.OrderId != 0)
            _dealIdByOrder[(deal.Login, deal.OrderId)] = deal.DealId;
    }

    /// <summary>
    /// Add multiple deals (e.g., from historical backfill).
    /// </summary>
    public void AddDeals(IEnumerable<ClosedDeal> deals)
    {
        foreach (var d in deals)
        {
            _deals[d.DealId] = d;
            if (d.Login != 0 && d.OrderId != 0)
                _dealIdByOrder[(d.Login, d.OrderId)] = d.DealId;
        }
    }

    /// <summary>
    /// Clear all deals (e.g., on reconnect for fresh backfill).
    /// </summary>
    public void Clear()
    {
        _deals.Clear();
        _dealIdByOrder.Clear();
    }

    /// <summary>
    /// Evict deals by id. Used by the reconciliation sweep to drop ghosts from the
    /// in-memory cache so DataSyncService doesn't re-upsert them to Supa 30s later.
    /// Returns the number of deals actually removed from the primary store.
    /// </summary>
    public int RemoveDeals(IEnumerable<ulong> dealIds)
    {
        var removed = 0;
        foreach (var id in dealIds)
        {
            if (_deals.TryRemove(id, out var d))
            {
                removed++;
                if (d.Login != 0 && d.OrderId != 0)
                    _dealIdByOrder.TryRemove((d.Login, d.OrderId), out _);
            }
        }
        return removed;
    }

    /// <summary>
    /// Get all stored deals.
    /// </summary>
    public IReadOnlyList<ClosedDeal> GetAllDeals() =>
        _deals.Values.ToList().AsReadOnly();

    /// <summary>
    /// Look up the MT5 deal ticket by (login, order ticket). Returns null when
    /// the deal hasn't been ingested yet or when OrderId wasn't captured (older rows).
    /// Used by the Bridge to map Centroid ext_order to the real B-Book deal number.
    /// </summary>
    public ulong? GetDealIdByOrder(ulong login, ulong orderId)
    {
        if (login == 0 || orderId == 0) return null;
        return _dealIdByOrder.TryGetValue((login, orderId), out var dealId) ? dealId : null;
    }

    /// <summary>
    /// Get realized P&L summary grouped by symbol.
    /// Volume includes ALL deals (IN + OUT) to match MT5 Manager totals.
    /// P&L only from OUT deals (only closing deals carry profit/loss).
    /// </summary>
    public IReadOnlyList<SymbolPnL> GetPnLBySymbol(DateTime? from = null, DateTime? to = null)
    {
        var allDeals = _deals.Values
            .Where(d => !string.IsNullOrEmpty(d.Symbol))
            .Where(d => from == null || d.Time >= from.Value)
            .Where(d => to == null || d.Time < to.Value.AddDays(1))
            .ToList();

        return allDeals
            .GroupBy(d => d.Symbol)
            .Select(g =>
            {
                var outDeals = g.Where(d => d.Entry == 1 || d.Entry == 2 || d.Entry == 3).ToList();
                return new SymbolPnL
                {
                    Symbol = g.Key,
                    DealCount = g.Count(),
                    // P&L only from OUT deals (closes carry profit)
                    TotalProfit = outDeals.Sum(d => d.Profit),
                    TotalCommission = g.Sum(d => d.Commission), // Commission on both IN + OUT
                    TotalSwap = outDeals.Sum(d => d.Swap),
                    TotalFee = g.Sum(d => d.Fee), // Fee on both IN + OUT
                    // Volume from ALL deals (IN + OUT) — matches MT5 Manager
                    TotalVolume = g.Sum(d => d.VolumeLots),
                    BuyVolume = g.Where(d => d.Direction == "BUY").Sum(d => d.VolumeLots),
                    SellVolume = g.Where(d => d.Direction == "SELL").Sum(d => d.VolumeLots)
                };
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
            .Where(d => !string.IsNullOrEmpty(d.Symbol))
            .GroupBy(d => d.Time.Date)
            .OrderByDescending(g => g.Key)
            .Select(dayGroup => new DailyPnL
            {
                Date = dayGroup.Key,
                DealCount = dayGroup.Count(),
                TotalProfit = dayGroup.Where(d => d.Entry == 1 || d.Entry == 2 || d.Entry == 3).Sum(d => d.Profit),
                TotalCommission = dayGroup.Sum(d => d.Commission),
                TotalSwap = dayGroup.Where(d => d.Entry == 1 || d.Entry == 2 || d.Entry == 3).Sum(d => d.Swap),
                TotalFee = dayGroup.Sum(d => d.Fee),
                Symbols = dayGroup
                    .GroupBy(d => d.Symbol)
                    .Select(sg =>
                    {
                        var outDeals = sg.Where(d => d.Entry == 1 || d.Entry == 2 || d.Entry == 3).ToList();
                        return new SymbolPnL
                        {
                            Symbol = sg.Key,
                            DealCount = sg.Count(),
                            TotalProfit = outDeals.Sum(d => d.Profit),
                            TotalCommission = sg.Sum(d => d.Commission),
                            TotalSwap = outDeals.Sum(d => d.Swap),
                            TotalFee = sg.Sum(d => d.Fee),
                            TotalVolume = sg.Sum(d => d.VolumeLots),
                            BuyVolume = sg.Where(d => d.Direction == "BUY").Sum(d => d.VolumeLots),
                            SellVolume = sg.Where(d => d.Direction == "SELL").Sum(d => d.VolumeLots)
                        };
                    })
                    .OrderByDescending(p => Math.Abs(p.NetPnL))
                    .ToList()
            })
            .ToList()
            .AsReadOnly();
    }

    public int DealCount => _deals.Count;
}
