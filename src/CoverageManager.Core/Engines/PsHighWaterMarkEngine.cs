using CoverageManager.Core.Models.EquityPnL;

namespace CoverageManager.Core.Engines;

/// <summary>
/// Reverse high-water-mark profit-share engine.
///
/// <para>The term "profit share" is a misnomer for how the desk actually uses
/// it — dealers pay out a % of <b>client losses</b> back to the client (or
/// receive a % from an LP on trading P&amp;L). The core mechanic is a
/// low-water mark on cumulative trading P&amp;L:</para>
///
/// <para>Per login contract:</para>
/// <list type="bullet">
///   <item><c>cum_pl</c> = running sum of monthly trading P&amp;L since the
///     contract started (net of any deposits/withdrawals/credits).</item>
///   <item><c>low_water_mark</c> = most-negative cum_pl ever seen. Starts at 0.</item>
/// </list>
///
/// <para>Each month-end:</para>
/// <list type="number">
///   <item>Add the month's trading P&amp;L to <c>cum_pl</c>.</item>
///   <item>If <c>cum_pl &lt; low_water_mark</c>, the account dropped to a new
///     low → pay PS on the incremental drawdown:
///     <c>PS_month = (low_water_mark − cum_pl) × pct/100</c>, then set
///     <c>low_water_mark = cum_pl</c>.</item>
///   <item>Otherwise no PS this month (recovery months don't earn new PS
///     until a new low is hit).</item>
/// </list>
///
/// <para>Example: 10% PS, starting cum_pl = 0, LWM = 0:</para>
/// <list type="table">
///   <listheader><term>Month</term><description>Monthly P&amp;L → cum_pl → PS → LWM</description></listheader>
///   <item><term>1</term><description>+25k → +25k → 0 → 0</description></item>
///   <item><term>2</term><description>−75k → −50k → 5k → −50k</description></item>
///   <item><term>3</term><description>+30k → −20k → 0 → −50k  (recovery, no PS)</description></item>
///   <item><term>4</term><description>−40k → −60k → 1k → −60k  (new low)</description></item>
/// </list>
///
/// <para>This engine is <b>stateful but idempotent per month</b>. Callers feed
/// it the current <see cref="EquityPnLClientConfig"/> (which contains the
/// running state) plus a list of monthly P&amp;L values keyed by
/// month-end. It returns the new state + the PS that accrued inside the
/// requested query window. The caller is responsible for persisting the
/// returned state back to Supabase so subsequent runs pick up where this
/// one left off.</para>
/// </summary>
public static class PsHighWaterMarkEngine
{
    /// <summary>
    /// Result of running the engine across a set of month-ends.
    /// </summary>
    public readonly record struct Result(
        decimal PsInWindow,
        decimal NewCumPl,
        decimal NewLowWaterMark,
        DateTime? NewLastProcessedMonth);

    /// <summary>
    /// Process month-ends in chronological order, advancing cum_pl and the
    /// low-water mark. Only months strictly greater than
    /// <c>config.PsLastProcessedMonth</c> are considered — this keeps the
    /// engine idempotent if the API is hit twice for overlapping ranges.
    ///
    /// The window filter (<paramref name="windowStart"/> .. <paramref name="windowEnd"/>)
    /// determines which months' PS payments count toward <c>PsInWindow</c>
    /// in the returned record (shown in the UI), but every processed month
    /// advances the engine's state regardless of whether it falls inside the
    /// visible window.
    /// </summary>
    /// <param name="config">Current per-login state. Not mutated.</param>
    /// <param name="monthlyPl">
    /// Month-end-keyed trading P&amp;L values in chronological order. The key
    /// is the last calendar day of the Asia/Beirut month (UTC-midnight of the
    /// month's end works too — only ordering matters).
    /// </param>
    /// <param name="windowStart">Inclusive start of the UI window (UTC instant).</param>
    /// <param name="windowEnd">Inclusive end of the UI window (UTC instant).</param>
    public static Result Process(
        EquityPnLClientConfig config,
        IReadOnlyList<(DateTime MonthEndUtc, decimal MonthlyPl)> monthlyPl,
        DateTime windowStart,
        DateTime windowEnd)
    {
        if (config.PsContractStart == null || config.PsPct <= 0m)
        {
            // PS disabled — window sum is 0 and state is unchanged.
            return new Result(0m, config.PsCumPl, config.PsLowWaterMark, config.PsLastProcessedMonth);
        }

        var pct = config.PsPct / 100m;
        var cumPl = config.PsCumPl;
        var lwm = config.PsLowWaterMark;
        var psInWindow = 0m;
        DateTime? lastProcessed = config.PsLastProcessedMonth;

        foreach (var (monthEndUtc, monthlyP) in monthlyPl.OrderBy(x => x.MonthEndUtc))
        {
            // Skip months that either pre-date the contract or have already been
            // processed on a prior run — the idempotency guard.
            if (monthEndUtc < config.PsContractStart.Value) continue;
            if (lastProcessed != null && monthEndUtc <= lastProcessed.Value) continue;

            cumPl += monthlyP;
            decimal psThisMonth = 0m;
            if (cumPl < lwm)
            {
                psThisMonth = (lwm - cumPl) * pct;
                lwm = cumPl;
            }

            // PS attributed to the window only if the month-end itself falls
            // inside the query range. Months before the window still advance
            // state but don't inflate the visible number.
            if (monthEndUtc >= windowStart && monthEndUtc <= windowEnd)
            {
                psInWindow += psThisMonth;
            }

            lastProcessed = monthEndUtc;
        }

        return new Result(psInWindow, cumPl, lwm, lastProcessed);
    }
}
