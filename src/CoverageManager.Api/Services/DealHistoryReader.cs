using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;

namespace CoverageManager.Api.Services;

/// <summary>The history for a requested range, and how it was assembled.</summary>
public sealed record DealHistoryResult(
    IReadOnlyList<ClosedDeal> Deals,
    DateTime RequestedFromUtc,
    DateTime EffectiveFromUtc,
    DateTime ToUtc,
    DateTime RetentionFloorUtc,
    bool ClampedByRetention,
    int FromStore,
    int FromWorkingSetOnly,
    int WorkingSetOverrides);

/// <summary>
/// Closed-deal history for a date range (V2_PLAN §9a, owner requirement): the 12-month closed-trades
/// look-back is served from LOCAL POSTGRES, never from the Live Bridge FeedBook, which is a 48 h working
/// set and cannot answer it.
///
/// <para>Assembly: every stored deal in [from, to) comes from Postgres; the in-memory working set
/// (<see cref="DealStore"/>, fed by the feed and drained into Postgres by DataSyncService every 30 s)
/// is overlaid on top by deal id. That covers the short tail the feed has delivered but persistence has
/// not yet written, and where both hold a deal the in-memory copy wins, because it is the value being
/// persisted and is never older than the stored row.</para>
///
/// <para>Nothing older than the retention floor is stored (V2_PLAN §5.5), so a request starting earlier
/// is clamped and says so (<see cref="DealHistoryResult.ClampedByRetention"/>). Deeper look-back is the
/// deferred, read-only accounting-system API -- not this.</para>
/// </summary>
public sealed class DealHistoryReader
{
    private readonly IDataStore _store;
    private readonly DealStore _workingSet;
    private readonly Func<DateTime> _utcNow;
    private readonly int _retentionMonths;

    public DealHistoryReader(IDataStore store, DealStore workingSet, IConfiguration config)
        : this(store, workingSet, config, () => DateTime.UtcNow) { }

    public DealHistoryReader(IDataStore store, DealStore workingSet, IConfiguration config, Func<DateTime> utcNow)
    {
        _store = store;
        _workingSet = workingSet;
        _utcNow = utcNow;
        // Reading may use a LONGER window than decided, never a shorter one: a shorter floor would hide
        // deals that are legitimately retained.
        _retentionMonths = Math.Max(RetentionPolicy.DecidedMonths,
                                    config.GetValue("Retention:Months", RetentionPolicy.DecidedMonths));
    }

    /// <summary>Trade and non-trade closed deals in [<paramref name="fromUtc"/>, <paramref name="toUtc"/>).</summary>
    public async Task<DealHistoryResult> GetClosedDealsAsync(DateTime fromUtc, DateTime toUtc)
    {
        var floor = RetentionPolicy.CutoffUtc(_utcNow(), _retentionMonths);
        var from = fromUtc < floor ? floor : fromUtc;
        var clamped = fromUtc < floor;
        if (toUtc <= from)
            return new DealHistoryResult(Array.Empty<ClosedDeal>(), fromUtc, from, toUtc, floor, clamped, 0, 0, 0);

        // GetDealsAsync's upper bound is inclusive; this range is half-open, so trim.
        var stored = (await _store.GetDealsAsync("bbook", from, toUtc))
            .Where(r => r.DealTime < toUtc)
            .Select(DealRecordMapper.ToClosedDeal)
            .ToList();

        var byId = new Dictionary<ulong, ClosedDeal>(stored.Count);
        foreach (var d in stored) byId[d.DealId] = d;

        int onlyInMemory = 0, overrides = 0;
        foreach (var d in _workingSet.GetAllDeals())
        {
            if (d.Time < from || d.Time >= toUtc) continue;
            if (byId.ContainsKey(d.DealId)) overrides++; else onlyInMemory++;
            byId[d.DealId] = d;
        }

        var deals = byId.Values.OrderBy(d => d.Time).ThenBy(d => d.DealId).ToList();
        return new DealHistoryResult(deals, fromUtc, from, toUtc, floor, clamped, stored.Count, onlyInMemory, overrides);
    }
}
