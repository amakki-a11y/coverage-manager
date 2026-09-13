namespace CoverageManager.Core.Engines;

/// <summary>
/// The v2 deal-retention rule (owner decision 2026-09-12, V2_PLAN §5.5): local Postgres keeps a
/// rolling 12 months of closed deals. One definition used by the nightly pruner and the history
/// reader, and equal to the SQL documented in db/README.md and used by the import tooling:
/// <c>(date_trunc('day', (now() AT TIME ZONE 'UTC')) - interval '12 months') AT TIME ZONE 'UTC'</c>.
/// The months are subtracted from the zone-less timestamp: subtracting from a timestamptz does the
/// month arithmetic in the session timezone and shifts the cutoff on month-end and DST-edge days.
/// A deal is retained iff <c>deal_time &gt;= cutoff</c>; the pruner deletes only <c>deal_time &lt; cutoff</c>.
/// </summary>
public static class RetentionPolicy
{
    /// <summary>The decided retention. Configuration may keep MORE, never less.</summary>
    public const int DecidedMonths = 12;

    /// <summary>
    /// UTC midnight of <paramref name="nowUtc"/>'s day, minus <paramref name="months"/> months. Month
    /// arithmetic clamps to the end of a shorter month exactly as Postgres' <c>interval 'N months'</c>
    /// does (e.g. 2027-03-31 minus 1 month = 2027-02-28).
    /// </summary>
    public static DateTime CutoffUtc(DateTime nowUtc, int months)
    {
        var u = nowUtc.Kind == DateTimeKind.Local ? nowUtc.ToUniversalTime() : nowUtc;
        return new DateTime(u.Year, u.Month, u.Day, 0, 0, 0, DateTimeKind.Utc).AddMonths(-months);
    }

    /// <summary>True when a configured retention would delete inside the decided 12-month window.</summary>
    public static bool IsBelowDecided(int months) => months < DecidedMonths;
}
