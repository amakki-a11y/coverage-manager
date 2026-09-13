using System.Collections.Concurrent;
using CoverageManager.Core.Models;

namespace CoverageManager.Core.Engines;

/// <summary>What <see cref="DealStore.AddDeal"/> did with a deal.</summary>
public enum DealStoreChange { Added, Updated, Unchanged }

/// <summary>A deal waiting to be persisted, with the version of the store entry it was taken from.</summary>
public readonly record struct PendingDeal(ClosedDeal Deal, long Version);

/// <summary>A deal that arrived with different values than the copy already held; the audit trail wants both.</summary>
public sealed record DealModification(ClosedDeal Before, ClosedDeal After, DateTime DetectedAtUtc);

/// <summary>Counts from one <see cref="DealStore.Prune"/> pass.</summary>
public readonly record struct DealPruneResult(int EvictedSynced, int EvictedUnsynced, int Remaining);

/// <summary>
/// In-memory store of closed deals. Tracks realized P&amp;L per symbol.
/// Thread-safe for concurrent deal callbacks and API reads.
///
/// <para>Persistence is incremental: every deal that arrives new or with changed values is queued as pending
/// (<see cref="TakePending"/>, <see cref="MarkSynced"/>), so the persister writes only what changed since its last
/// successful write instead of the whole store. Deals read back from the durable store enter through
/// <see cref="Load"/> and are never queued. Value changes are also queued as <see cref="DealModification"/>s for the
/// audit trail, so no read of the durable store is needed to detect a dealer edit. The store is bounded by
/// <see cref="Prune"/>: deals older than the retention window are forgotten once persisted (the durable store keeps
/// them), and a hard cap protects memory during a long outage of the durable store.</para>
/// </summary>
public class DealStore
{
    private readonly ConcurrentDictionary<ulong, ClosedDeal> _deals = new();
    // Secondary index: (login, orderId) -> dealId. Used by the Bridge to resolve Centroid ext_order -> true MT5 deal number.
    private readonly ConcurrentDictionary<(ulong login, ulong orderId), ulong> _dealIdByOrder = new();
    // Deals not yet persisted: id -> version of the entry when it last changed. The version lets the persister mark a
    // deal synced without losing a change that arrived while its batch was in flight.
    private readonly ConcurrentDictionary<ulong, long> _pending = new();
    private long _version;
    // Bumped whenever the set of deals or a deal's values change: the per-symbol P&L cache keys on it.
    private long _changeVersion;
    private IReadOnlyList<SymbolPnL>? _todayPnLCache;
    private long _todayPnLCacheVersion = -1;
    private long _todayPnLCacheAtTicks;
    private DateTime _todayPnLCacheDay;
    private readonly ConcurrentQueue<DealModification> _modifications = new();
    private int _modificationCount;
    private const int MaxQueuedModifications = 10_000;
    // Earliest deal time currently held in memory (UTC). Used by callers that
    // need to know whether the in-memory cache covers a historical query window
    // before relying on its aggregates. `null` when the store is empty.
    private long _earliestDealTimeTicks = long.MaxValue;

    /// <summary>
    /// Earliest <c>DealTime</c> across all deals currently in the store, UTC.
    /// Returns <c>null</c> when the store is empty. Equity-P&amp;L's NetDepW
    /// override path uses this to decide whether DealStore's per-login sum
    /// covers the requested window, falling back to Supabase otherwise so a
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

    public int DealCount => _deals.Count;

    /// <summary>Deals added or changed since they were last marked synced.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>Value changes queued for the audit trail.</summary>
    public int PendingModificationCount => Volatile.Read(ref _modificationCount);

    private void TrackEarliest(DateTime dealTime)
    {
        var newTicks = AsUtc(dealTime).Ticks;
        long current;
        do
        {
            current = Interlocked.Read(ref _earliestDealTimeTicks);
            if (newTicks >= current) return;
        } while (Interlocked.CompareExchange(ref _earliestDealTimeTicks, newTicks, current) != current);
    }

    /// <summary>
    /// Add a deal (deduplicated by DealId). A new deal, or one whose values differ from the copy held, is queued for
    /// persistence; the same deal twice is a no-op.
    /// </summary>
    public DealStoreChange AddDeal(ClosedDeal deal) => Add(deal, queue: true);

    /// <summary>Add multiple deals (e.g., from a backfill); each is queued for persistence like <see cref="AddDeal"/>.</summary>
    public void AddDeals(IEnumerable<ClosedDeal> deals)
    {
        foreach (var d in deals) Add(d, queue: true);
    }

    /// <summary>
    /// Deals read back from the durable store on startup: held for the aggregations but not queued for persistence,
    /// and never overwriting a deal already held (the live source is authoritative). Returns how many were new.
    /// </summary>
    public int Load(IEnumerable<ClosedDeal> deals)
    {
        var added = 0;
        foreach (var d in deals)
            if (Add(d, queue: false) == DealStoreChange.Added) added++;
        return added;
    }

    private DealStoreChange Add(ClosedDeal deal, bool queue)
    {
        var change = DealStoreChange.Unchanged;
        ClosedDeal? before = null;
        if (queue)
        {
            _deals.AddOrUpdate(deal.DealId,
                _ => { change = DealStoreChange.Added; return deal; },
                (_, existing) =>
                {
                    if (SameValues(existing, deal)) { change = DealStoreChange.Unchanged; return existing; }
                    before = existing;
                    change = DealStoreChange.Updated;
                    return deal;
                });
        }
        else if (_deals.TryAdd(deal.DealId, deal))
        {
            change = DealStoreChange.Added;
        }

        if (change == DealStoreChange.Unchanged) return change;
        Interlocked.Increment(ref _changeVersion);

        if (deal.Login != 0 && deal.OrderId != 0)
            _dealIdByOrder[(deal.Login, deal.OrderId)] = deal.DealId;
        TrackEarliest(deal.Time);

        if (queue)
        {
            _pending[deal.DealId] = Interlocked.Increment(ref _version);
            if (change == DealStoreChange.Updated && before is not null)
                Enqueue(new DealModification(before, deal, DateTime.UtcNow));
        }
        return change;
    }

    private void Enqueue(DealModification modification)
    {
        _modifications.Enqueue(modification);
        if (Interlocked.Increment(ref _modificationCount) > MaxQueuedModifications && _modifications.TryDequeue(out _))
            Interlocked.Decrement(ref _modificationCount);
    }

    // ---- persistence queue ----------------------------------------------------------------

    /// <summary>
    /// Up to <paramref name="max"/> pending deals, oldest first, each with the version it was taken at. The caller
    /// persists them and calls <see cref="MarkSynced"/>; a deal that changes again meanwhile keeps a newer version
    /// and stays pending.
    /// </summary>
    public IReadOnlyList<PendingDeal> TakePending(int max)
    {
        if (max <= 0 || _pending.IsEmpty) return Array.Empty<PendingDeal>();
        var list = new List<PendingDeal>(Math.Min(max, _pending.Count));
        foreach (var kv in _pending)
        {
            if (_deals.TryGetValue(kv.Key, out var deal)) list.Add(new PendingDeal(deal, kv.Value));
            else _pending.TryRemove(kv);   // removed from the store meanwhile: nothing to persist
        }
        list.Sort(static (a, b) =>
        {
            var c = AsUtc(a.Deal.Time).CompareTo(AsUtc(b.Deal.Time));
            return c != 0 ? c : a.Deal.DealId.CompareTo(b.Deal.DealId);
        });
        return list.Count <= max ? list : list.GetRange(0, max);
    }

    /// <summary>Marks deals persisted; one that changed since it was taken (a newer version) stays pending. Returns how many were cleared.</summary>
    public int MarkSynced(IEnumerable<PendingDeal> synced)
    {
        var cleared = 0;
        foreach (var p in synced)
            if (_pending.TryRemove(new KeyValuePair<ulong, long>(p.Deal.DealId, p.Version))) cleared++;
        return cleared;
    }

    /// <summary>Drains the value changes queued for the audit trail.</summary>
    public IReadOnlyList<DealModification> TakeModifications()
    {
        var list = new List<DealModification>();
        while (_modifications.TryDequeue(out var m))
        {
            Interlocked.Decrement(ref _modificationCount);
            list.Add(m);
        }
        return list;
    }

    /// <summary>Puts modifications back (their audit write failed); they are retried on the next pass.</summary>
    public void RequeueModifications(IEnumerable<DealModification> modifications)
    {
        foreach (var m in modifications) Enqueue(m);
    }

    /// <summary>
    /// Forgets persisted deals older than <paramref name="olderThanUtc"/> (the durable store keeps them). Deals still
    /// pending are kept whatever their age, unless the store exceeds <paramref name="hardCap"/> deals, in which case
    /// the oldest go first, pending or not (reported as <see cref="DealPruneResult.EvictedUnsynced"/>).
    /// </summary>
    public DealPruneResult Prune(DateTime olderThanUtc, int hardCap)
    {
        var cutoff = AsUtc(olderThanUtc);
        var evictedSynced = 0;
        var evictedUnsynced = 0;
        foreach (var kv in _deals)
        {
            if (AsUtc(kv.Value.Time) >= cutoff) continue;
            if (_pending.ContainsKey(kv.Key)) continue;
            if (Remove(kv.Key)) evictedSynced++;
        }
        if (hardCap > 0 && _deals.Count > hardCap)
        {
            var excess = _deals.Count - hardCap;
            var oldest = _deals.Values.OrderBy(d => AsUtc(d.Time)).ThenBy(d => d.DealId).Take(excess).ToList();
            foreach (var d in oldest)
            {
                var wasPending = _pending.TryRemove(d.DealId, out _);
                if (!Remove(d.DealId)) continue;
                if (wasPending) evictedUnsynced++; else evictedSynced++;
            }
        }
        if (evictedSynced + evictedUnsynced > 0) RecomputeEarliest();
        return new DealPruneResult(evictedSynced, evictedUnsynced, _deals.Count);
    }

    /// <summary>
    /// Clear all deals (e.g., on reconnect for fresh backfill).
    /// </summary>
    public void Clear()
    {
        _deals.Clear();
        _dealIdByOrder.Clear();
        _pending.Clear();
        while (_modifications.TryDequeue(out _)) { }
        Interlocked.Exchange(ref _modificationCount, 0);
        Interlocked.Exchange(ref _earliestDealTimeTicks, long.MaxValue);
        Interlocked.Increment(ref _changeVersion);
    }

    /// <summary>Changes every time the set of deals or a deal's values change; consumers can cache on it.</summary>
    public long ChangeVersion => Interlocked.Read(ref _changeVersion);

    /// <summary>
    /// Today's (UTC) realized P&amp;L per symbol for the live broadcast, recomputed only when the store changed and at
    /// most once per <paramref name="maxAge"/>. The broadcast runs up to ten times a second; with two days of feed
    /// deals in memory, recomputing over every deal per frame would pin a core.
    /// </summary>
    public IReadOnlyList<SymbolPnL> GetTodayPnLBySymbol(TimeSpan maxAge)
    {
        var today = DateTime.UtcNow.Date;
        var version = Interlocked.Read(ref _changeVersion);
        var now = DateTime.UtcNow.Ticks;
        var cache = Volatile.Read(ref _todayPnLCache);
        if (cache is not null && _todayPnLCacheDay == today
            && (Interlocked.Read(ref _todayPnLCacheVersion) == version || now - Interlocked.Read(ref _todayPnLCacheAtTicks) < maxAge.Ticks))
            return cache;

        var fresh = GetPnLBySymbol(from: today);
        _todayPnLCacheDay = today;
        Interlocked.Exchange(ref _todayPnLCacheVersion, version);
        Interlocked.Exchange(ref _todayPnLCacheAtTicks, now);
        Volatile.Write(ref _todayPnLCache, fresh);
        return fresh;
    }

    /// <summary>
    /// Evict deals by id. Used by the reconciliation sweep to drop ghosts from the
    /// in-memory cache so the persister doesn't re-upsert them 30s later.
    /// Returns the number of deals actually removed from the primary store.
    /// </summary>
    public int RemoveDeals(IEnumerable<ulong> dealIds)
    {
        var removed = 0;
        foreach (var id in dealIds)
        {
            _pending.TryRemove(id, out _);
            if (Remove(id)) removed++;
        }
        if (removed > 0) RecomputeEarliest();
        return removed;
    }

    private bool Remove(ulong dealId)
    {
        if (!_deals.TryRemove(dealId, out var d)) return false;
        if (d.Login != 0 && d.OrderId != 0)
            _dealIdByOrder.TryRemove((d.Login, d.OrderId), out _);
        Interlocked.Increment(ref _changeVersion);
        return true;
    }

    private void RecomputeEarliest()
    {
        var min = long.MaxValue;
        foreach (var d in _deals.Values)
        {
            var t = AsUtc(d.Time).Ticks;
            if (t < min) min = t;
        }
        Interlocked.Exchange(ref _earliestDealTimeTicks, min);
    }

    /// <summary>The fields the durable store holds; a deal re-sent with the same values is not a change.</summary>
    public static bool SameValues(ClosedDeal a, ClosedDeal b) =>
        a.DealId == b.DealId && a.Login == b.Login && a.Action == b.Action && a.Entry == b.Entry
        && string.Equals(a.Symbol, b.Symbol, StringComparison.Ordinal)
        && string.Equals(a.Direction, b.Direction, StringComparison.Ordinal)
        && a.VolumeLots == b.VolumeLots && a.Price == b.Price && a.Profit == b.Profit
        && a.Commission == b.Commission && a.Swap == b.Swap && a.Fee == b.Fee
        && a.OrderId == b.OrderId && a.PositionId == b.PositionId
        && AsUtc(a.Time) == AsUtc(b.Time);

    /// <summary>Deal times are compared as instants: the durable store hands back Kind=Local on a non-UTC box.</summary>
    private static DateTime AsUtc(DateTime t) => t.Kind switch
    {
        DateTimeKind.Utc => t,
        DateTimeKind.Local => t.ToUniversalTime(),
        _ => DateTime.SpecifyKind(t, DateTimeKind.Utc),
    };

    // ---- queries --------------------------------------------------------------------------

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
    /// Get realized P&L summary grouped by symbol, optionally limited to [from, to + 1 day).
    /// Rules live in <see cref="DealPnLAggregator"/> so this and the Postgres-backed history reader agree.
    /// </summary>
    public IReadOnlyList<SymbolPnL> GetPnLBySymbol(DateTime? from = null, DateTime? to = null) =>
        DealPnLAggregator.BySymbol(_deals.Values
            .Where(d => from == null || d.Time >= from.Value)
            .Where(d => to == null || d.Time < to.Value.AddDays(1)));

    /// <summary>Get realized P&L grouped by date then symbol (rules in <see cref="DealPnLAggregator"/>).</summary>
    public IReadOnlyList<DailyPnL> GetPnLByDay() => DealPnLAggregator.ByDay(_deals.Values);

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
