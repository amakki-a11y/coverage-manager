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
    // Earliest deal time currently held in memory (UTC). Used by callers that
    // need to know whether the in-memory cache covers a historical query window
    // before relying on its aggregates. `null` when the store is empty.
    private long _earliestDealTimeTicks = long.MaxValue;

    /// <summary>
    /// Earliest <c>DealTime</c> across all deals currently in the store, UTC.
    /// Returns <c>null</c> when the store is empty. Equity-P&amp;L's NetDepW
    /// override path uses this to decide whether DealStore's per-login sum
    /// covers the requested window — falling back to Supabase otherwise so a
    /// partial in-memory slice never overrides a complete historical sum.
    /// </summary>
    public DateTime? EarliestDealTime
    {
        get
        {
            var ticks = Interlocked.Read(ref _earliestDealTimeTicks);
            return ticks == long.MaxValue ? null : new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    private void TrackEarliest(DateTime dealTime)
    {
        var newTicks = dealTime.ToUniversalTime().Ticks;
        long current;
        do
        {
            current = Interlocked.Read(ref _earliestDealTimeTicks);
            if (newTicks >= current) return;
        } while (Interlocked.CompareExchange(ref _earliestDealTimeTicks, newTicks, current) != current);
    }

    /// <summary>
    /// Add a deal (deduplicated by DealId).
    /// </summary>
    public void AddDeal(ClosedDeal deal)
    {
        _deals[deal.DealId] = deal;
        if (deal.Login != 0 && deal.OrderId != 0)
            _dealIdByOrder[(deal.Login, deal.OrderId)] = deal.DealId;
        TrackEarliest(deal.Time);
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
            TrackEarliest(d.Time);
        }
    }

    /// <summary>
    /// Clear all deals (e.g., on reconnect for fresh backfill).
    /// </summary>
    public void Clear()
    {
        _deals.Clear();
        _dealIdByOrder.Clear();
        Interlocked.Exchange(ref _earliestDealTimeTicks, long.MaxValue);
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
            // Trade deals only — balance (2), credit (3), correction (5) etc
            // are now persisted in DealStore so the Equity P&L feature can see
            // them via Supabase, but they must not pollute symbol-level
            // aggregations. Action 0 = BUY, 1 = SELL; anything else is a
            // bookkeeping entry with no meaningful symbol/price.
            .Where(d => d.Action < 2)
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
            // Trade deals only — see `GetPnLBySymbol` for rationale.
            .Where(d => d.Action < 2)
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

    /// <summary>
    /// Per-login trade-balance flow for the Equity P&amp;L reconciliation path.
    /// Sums <c>Profit + Commission + Swap + Fee</c> across every **trade** deal
    /// in the window — matching the <c>action NOT IN (2, 3)</c> filter in the
    /// Supabase aggregator <c>SumTradeBalanceFlowPerLoginAsync</c>.
    ///
    /// <para>Fresher than the Supabase path — DealStore is updated on every
    /// MT5 deal callback (sub-100 ms), whereas DataSyncService only flushes
    /// to Supabase every 30 s. On active accounts the lag produced a visible
    /// Net Dep/W "flicker" (value jumps up when a trade closes, settles back
    /// down when the deal lands in Supabase). Callers should prefer this
    /// method and fall back to Supabase only when DealStore is cold (e.g.
    /// just after a backend restart before the Supabase backfill completes).</para>
    ///
    /// <para>Filters: <c>Action &lt; 2</c> keeps BUY / SELL (0, 1); values
    /// 4 (CHARGE), 5 (CORRECTION), 6 (BONUS), 7+ (commission variants) are
    /// included too — we filter OUT balance (2) and credit (3), leaving
    /// everything else as "trade flow" consistent with the Supabase side.</para>
    /// </summary>
    /// <param name="from">Inclusive UTC start of window.</param>
    /// <param name="to">Exclusive UTC end of window.</param>
    public IReadOnlyDictionary<long, decimal> SumTradeBalanceFlowPerLogin(DateTime from, DateTime to)
    {
        var result = new Dictionary<long, decimal>();
        foreach (var d in _deals.Values)
        {
            if (d.Time < from || d.Time >= to) continue;
            // Mirror Supabase filter: action NOT IN (2, 3).
            if (d.Action == 2 || d.Action == 3) continue;
            var flow = d.Profit + d.Commission + d.Swap + d.Fee;
            if (flow == 0m) continue;
            // MT5 logins fit comfortably in signed 32-bit; cast ulong → long
            // here so the return type matches the Supabase aggregator's
            // Dictionary<long, decimal> contract.
            var key = (long)d.Login;
            result[key] = (result.TryGetValue(key, out var cur) ? cur : 0m) + flow;
        }
        return result;
    }
}
