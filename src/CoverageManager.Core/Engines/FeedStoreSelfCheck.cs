using CoverageManager.Core.Models;

namespace CoverageManager.Core.Engines;

/// <summary>
/// Outcome of comparing the feed's working set with the store for the retained window.
/// There is deliberately NO delete list: a stored deal the feed does not hold is only
/// counted (<see cref="StoreOnly"/>). The feed keeps a short retention window and cannot
/// prove a deal no longer exists, so the self-check heals by re-writing and never removes.
/// </summary>
public sealed record SelfCheckPlan(
    bool Skipped,
    DateTime FromUtc,
    DateTime ToUtc,
    int FeedDealCount,
    int StoredDealCount,
    IReadOnlyList<DealRecord> Missing,
    IReadOnlyList<DealRecord> Modified,
    int StoreOnly,
    string Note)
{
    /// <summary>Rows to re-write from the feed: missing ones plus corrected modified ones.</summary>
    public IReadOnlyList<DealRecord> Rewrite => Missing.Concat(Modified).ToList();
}

/// <summary>
/// The v2 feed&lt;-&gt;store self-check (V2_PLAN §5.4). Replaces v1's cross-source reconciliation
/// sweep: in v2 the Live Bridge feed is the only authority and Postgres persists it, so the only
/// residual risk is a record lost between "received" and "committed". This compares the feed's
/// working set against the store, re-writes what diverged, and never deletes.
/// </summary>
public static class FeedStoreSelfCheck
{
    /// <summary>
    /// Plans a self-check over trade deals (action &lt; 2 -- the feed query returns trade deals only,
    /// so the stored side is filtered the same way or every cash movement would read as store-only).
    /// </summary>
    /// <param name="feedDeals">Deals from the feed's working set, already mapped with <see cref="DealRecordMapper"/>.</param>
    /// <param name="storedDeals">Stored deals covering at least the window.</param>
    /// <param name="window">How far back the feed's record reaches. Only that span is compared.</param>
    /// <param name="nowUtc">Current time; the window runs to now + <paramref name="aheadBuffer"/> because deal stamps
    /// are the MT5 server clock (UTC+3 here), so the newest deals sit ahead of UTC.</param>
    /// <param name="fullLookback">Span used when the provider reports its full history (not the case under the feed).</param>
    /// <param name="aheadBuffer">How far past now the window extends.</param>
    public static SelfCheckPlan Plan(
        IReadOnlyList<DealRecord> feedDeals,
        IReadOnlyList<DealRecord> storedDeals,
        DealHistoryWindow window,
        DateTime nowUtc,
        TimeSpan fullLookback,
        TimeSpan aheadBuffer)
    {
        var toUtc = nowUtc + aheadBuffer;
        if (window.IsEmpty)
        {
            return new SelfCheckPlan(true, nowUtc, toUtc, 0, 0,
                Array.Empty<DealRecord>(), Array.Empty<DealRecord>(), 0,
                "skipped: the feed holds no deals yet, so there is nothing to compare");
        }

        var fromUtc = window.Complete ? nowUtc - fullLookback : window.FromUtc!.Value;
        bool InWindow(DealRecord d) => d.DealTime >= fromUtc && d.DealTime < toUtc;
        static bool IsTrade(DealRecord d) => d.Action < 2;

        var feed = feedDeals.Where(d => IsTrade(d) && InWindow(d))
                            .GroupBy(Key).Select(g => g.Last()).ToList();
        var stored = storedDeals.Where(d => IsTrade(d) && InWindow(d))
                                .GroupBy(Key).ToDictionary(g => g.Key, g => g.Last());

        var missing = new List<DealRecord>();
        var modified = new List<DealRecord>();
        foreach (var f in feed)
        {
            if (!stored.TryGetValue(Key(f), out var s)) { missing.Add(f); continue; }
            if (SameValues(f, s)) continue;

            // Heal the values, never re-key: keep the stored canonical_symbol. It is derived from
            // the symbol mappings at write time; changing it here would silently move a historical
            // deal between aggregation rows whenever a mapping was edited.
            modified.Add(Clone(f, canonicalSymbol: s.CanonicalSymbol));
        }

        var feedKeys = feed.Select(Key).ToHashSet();
        var storeOnly = stored.Keys.Count(k => !feedKeys.Contains(k));

        var note = $"window {fromUtc:O} .. {toUtc:O}; feed {feed.Count}, store {stored.Count}; " +
                   $"re-wrote {missing.Count} missing + {modified.Count} modified; " +
                   $"{storeOnly} store-only kept (never deleted)";
        return new SelfCheckPlan(false, fromUtc, toUtc, feed.Count, stored.Count, missing, modified, storeOnly, note);
    }

    private static (string Source, long DealId) Key(DealRecord d) => (d.Source, d.DealId);

    /// <summary>The fields the feed is authoritative for. canonical_symbol is excluded on purpose (see above).</summary>
    public static bool SameValues(DealRecord a, DealRecord b) =>
        a.Login == b.Login &&
        string.Equals(a.Symbol, b.Symbol, StringComparison.Ordinal) &&
        a.Action == b.Action &&
        a.Entry == b.Entry &&
        a.Volume == b.Volume &&
        a.Price == b.Price &&
        a.Profit == b.Profit &&
        a.Commission == b.Commission &&
        a.Swap == b.Swap &&
        a.Fee == b.Fee &&
        a.OrderId == b.OrderId &&
        a.PositionId == b.PositionId &&
        a.DealTime.ToUniversalTime() == b.DealTime.ToUniversalTime();

    private static DealRecord Clone(DealRecord d, string canonicalSymbol) => new()
    {
        Source = d.Source,
        DealId = d.DealId,
        Login = d.Login,
        Symbol = d.Symbol,
        CanonicalSymbol = canonicalSymbol,
        Direction = d.Direction,
        Action = d.Action,
        Entry = d.Entry,
        Volume = d.Volume,
        Price = d.Price,
        Profit = d.Profit,
        Commission = d.Commission,
        Swap = d.Swap,
        Fee = d.Fee,
        OrderId = d.OrderId,
        PositionId = d.PositionId,
        DealTime = d.DealTime,
    };
}
