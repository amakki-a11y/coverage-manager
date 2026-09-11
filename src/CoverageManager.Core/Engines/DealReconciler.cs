using CoverageManager.Core.Models;

namespace CoverageManager.Core.Engines;

/// <summary>
/// What a deal comparison may do, after the requested window has been clamped to the provider's
/// <see cref="DealHistoryWindow"/>. Pure data; the caller performs the writes.
/// </summary>
public sealed class DealReconciliationPlan
{
    /// <summary>The start the caller asked for.</summary>
    public required DateTime RequestedFromUtc { get; init; }

    /// <summary>The start actually compared: the requested one, or the provider's window start when that is later.</summary>
    public required DateTime FromUtc { get; init; }

    public required DateTime ToUtc { get; init; }

    public required DealHistoryWindow History { get; init; }

    /// <summary>The provider's deals inside the compared window.</summary>
    public required IReadOnlyList<ClosedDeal> ProviderDeals { get; init; }

    /// <summary>The stored deals inside the compared window: the ones the provider can vouch for.</summary>
    public required IReadOnlyList<DealRecord> StoredDeals { get; init; }

    /// <summary>The provider has them, the store lacks them: to backfill.</summary>
    public required IReadOnlyList<ClosedDeal> Missing { get; init; }

    /// <summary>Present on both sides: candidates for the modification check.</summary>
    public required IReadOnlyList<ClosedDeal> Common { get; init; }

    /// <summary>Stored inside the compared window but absent from the provider: ghosts, safe to delete.</summary>
    public required IReadOnlyList<DealRecord> Ghosts { get; init; }

    /// <summary>
    /// Stored before the provider's window: the provider cannot say whether they still exist, so they are kept
    /// untouched and are not reported as extra.
    /// </summary>
    public required IReadOnlyList<DealRecord> Unverifiable { get; init; }

    /// <summary>What the clamp did, for run notes and API responses; empty when the window was not clamped.</summary>
    public required string Note { get; init; }

    public bool Clamped => FromUtc != RequestedFromUtc;

    /// <summary>False when the compared window is empty: the provider holds nothing, or nothing before the requested end.</summary>
    public bool Comparable => FromUtc < ToUtc;
}

/// <summary>
/// Decides what a comparison of the provider's deals with the stored (Supabase) deals may conclude.
/// With the Manager API the provider returns the server's full history, so every stored deal it lacks is a ghost.
/// With the Live Bridge feed the provider holds only the deals received since its resume point (a retention window),
/// so the comparison is limited to the feed's window: a stored deal older than the earliest deal the feed holds is
/// never a ghost - it is outside what the feed can answer - and it is kept.
/// </summary>
public static class DealReconciler
{
    public static DealReconciliationPlan Plan(
        IEnumerable<ClosedDeal> providerDeals,
        IEnumerable<DealRecord> storedDeals,
        DateTime fromUtc,
        DateTime toUtc,
        DealHistoryWindow history)
    {
        var requestedFrom = AsUtc(fromUtc);
        var to = AsUtc(toUtc);
        var from = requestedFrom;
        var provider = providerDeals.ToList();
        var stored = new List<DealRecord>();
        var unverifiable = new List<DealRecord>();
        string note;

        if (history.Complete)
        {
            // The server's whole history: every stored deal is comparable, exactly as before the feed existed.
            stored.AddRange(storedDeals);
            note = "";
        }
        else if (history.FromUtc is { } holds)
        {
            if (holds > from) from = holds;
            if (from > to) from = to;
            foreach (var d in storedDeals)
            {
                if (AsUtc(d.DealTime) >= from) stored.Add(d);
                else unverifiable.Add(d);
            }
            provider = provider.Where(d => AsUtc(d.Time) >= from).ToList();
            note = from == requestedFrom
                ? ""
                : $"the provider holds deals since {holds:O}: compared from there instead of {requestedFrom:O}; {unverifiable.Count} stored deals before it kept unchecked";
        }
        else
        {
            from = to;
            unverifiable.AddRange(storedDeals);
            provider.Clear();
            note = $"the provider holds no deals: nothing compared, nothing deleted; {unverifiable.Count} stored deals kept";
        }

        var storedIds = new HashSet<long>(stored.Select(d => d.DealId));
        foreach (var d in unverifiable) storedIds.Add(d.DealId);
        var providerIds = new HashSet<long>(provider.Select(d => (long)d.DealId));

        return new DealReconciliationPlan
        {
            RequestedFromUtc = requestedFrom,
            FromUtc = from,
            ToUtc = to,
            History = history,
            ProviderDeals = provider,
            StoredDeals = stored,
            Missing = provider.Where(d => !storedIds.Contains((long)d.DealId)).ToList(),
            Common = provider.Where(d => storedIds.Contains((long)d.DealId)).ToList(),
            Ghosts = stored.Where(d => !providerIds.Contains(d.DealId)).ToList(),
            Unverifiable = unverifiable,
            Note = note,
        };
    }

    /// <summary>
    /// Supabase timestamps deserialize with Kind=Local on a box whose zone is not UTC; everything is compared as UTC.
    /// An unspecified kind is taken as UTC, the store's convention.
    /// </summary>
    private static DateTime AsUtc(DateTime t) => t.Kind switch
    {
        DateTimeKind.Utc => t,
        DateTimeKind.Local => t.ToUniversalTime(),
        _ => DateTime.SpecifyKind(t, DateTimeKind.Utc),
    };
}
