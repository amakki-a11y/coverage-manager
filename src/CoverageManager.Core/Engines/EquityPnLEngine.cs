using CoverageManager.Core.Models;
using CoverageManager.Core.Models.EquityPnL;

namespace CoverageManager.Core.Engines;

/// <summary>
/// Classifies deals and equity snapshots into the 12 columns rendered on the
/// Equity P&amp;L tab. Pure logic — the orchestration layer (controller or
/// service) supplies already-fetched snapshot + deal + config data.
///
/// <para>Column formulas:</para>
/// <code>
/// Supposed Eq   = Begin + NetDepW + NetCred
/// PL            = CurrentEq − Supposed Eq
/// NetPL (client) = PL − CommReb − SpreadReb − Adj − PS
/// NetPL (cov)    = PL + CommReb + SpreadReb + Adj + PS
/// Broker Edge    = −Σ(Clients.NetPL) + Σ(Coverage.NetPL)
/// </code>
///
/// <para>Deal classification by MT5 <c>DealAction</c>:</para>
/// <list type="bullet">
///   <item><c>0/1 BUY/SELL</c> — trade deals; drive CommReb &amp; SpreadReb.</item>
///   <item><c>2 BALANCE</c> — deposits (+) / withdrawals (−) → NetDepW.</item>
///   <item><c>3 CREDIT</c> — NetCred.</item>
///   <item><c>5 CORRECTION</c> — Adj.</item>
///   <item>Other actions (<c>4 CHARGE</c>, <c>6 BONUS</c>, <c>7 COMMISSION</c>)
///     fall through to Adj for now — the dealer can override with configurable
///     rules in Phase 2.</item>
/// </list>
/// </summary>
public static class EquityPnLEngine
{
    /// <summary>
    /// Build a per-login <see cref="EquityPnLRow"/> from the pieces the
    /// orchestrator has already fetched. Does NOT persist PS state — callers
    /// must write the returned <paramref name="updatedConfig"/> back to
    /// Supabase if non-null (indicating the HWM engine advanced).
    /// </summary>
    public static EquityPnLRow BuildRow(
        TradingAccount account,
        decimal? beginEquity,
        decimal currentEquity,
        bool currentIsLive,
        IReadOnlyList<ClosedDeal> allDealsInWindow,
        EquityPnLClientConfig? config,
        IReadOnlyDictionary<string, decimal> spreadRates,
        Func<string, string> canonicalize,
        IReadOnlyList<(DateTime MonthEndUtc, decimal MonthlyPl)>? monthlyPlForPs,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        out EquityPnLClientConfig? updatedConfig)
    {
        updatedConfig = null;

        var row = new EquityPnLRow
        {
            Login = account.Login,
            Source = account.Source,
            Name = account.Name,
            Group = account.GroupName,
            CurrentEquity = currentEquity,
            CurrentIsLive = currentIsLive,
            BeginFromSnapshot = beginEquity.HasValue,
            BeginEquity = beginEquity ?? 0m,
        };

        foreach (var d in allDealsInWindow)
        {
            switch (d.Action)
            {
                case 0: // BUY
                case 1: // SELL
                {
                    // Comm rebate: only on deals where MT5 booked a negative
                    // commission (client was charged). Config-driven pct.
                    if (config != null && config.CommRebatePct > 0m && d.Commission < 0m)
                    {
                        row.CommRebate += Math.Abs(d.Commission) * config.CommRebatePct / 100m;
                    }
                    // Spread rebate: per-symbol rate × absolute volume.
                    var canonical = canonicalize(d.Symbol);
                    if (spreadRates.TryGetValue(canonical, out var rate) && rate > 0m)
                    {
                        row.SpreadRebate += Math.Abs(d.VolumeLots) * rate;
                    }
                    break;
                }
                case 2: // BALANCE (deposits / withdrawals)
                    row.NetDepositWithdraw += d.Profit;
                    break;
                case 3: // CREDIT
                    row.NetCredit += d.Profit;
                    break;
                case 5: // CORRECTION
                case 4: // CHARGE
                case 6: // BONUS
                case 7: // COMMISSION (balance-deal form, distinct from trade-deal commission)
                    row.Adjustment += d.Profit;
                    break;
            }
        }

        // PS — advances HWM state across all un-processed months up through
        // the window end, then sums up whatever falls inside the window.
        if (config != null && monthlyPlForPs != null && monthlyPlForPs.Count > 0)
        {
            var hwm = PsHighWaterMarkEngine.Process(config, monthlyPlForPs, windowStartUtc, windowEndUtc);
            row.ProfitShare = hwm.PsInWindow;

            var advanced =
                hwm.NewCumPl != config.PsCumPl ||
                hwm.NewLowWaterMark != config.PsLowWaterMark ||
                hwm.NewLastProcessedMonth != config.PsLastProcessedMonth;
            if (advanced)
            {
                updatedConfig = new EquityPnLClientConfig
                {
                    Login = config.Login,
                    Source = config.Source,
                    CommRebatePct = config.CommRebatePct,
                    PsPct = config.PsPct,
                    PsContractStart = config.PsContractStart,
                    PsCumPl = hwm.NewCumPl,
                    PsLowWaterMark = hwm.NewLowWaterMark,
                    PsLastProcessedMonth = hwm.NewLastProcessedMonth,
                    Notes = config.Notes,
                };
            }
        }

        // Derived columns.
        row.SupposedEquity = row.BeginEquity + row.NetDepositWithdraw + row.NetCredit;
        row.Pl             = row.CurrentEquity - row.SupposedEquity;

        // Net PL convention differs by side: client rebates/PS are broker
        // outlays (subtract); coverage rebates/PS are broker income (add).
        var nonTrading = row.CommRebate + row.SpreadRebate + row.Adjustment + row.ProfitShare;
        row.NetPl = account.Source == "coverage"
            ? row.Pl + nonTrading
            : row.Pl - nonTrading;

        return row;
    }

    /// <summary>Sum rows into a totals pseudo-row (name/login left blank).</summary>
    public static EquityPnLRow Total(IEnumerable<EquityPnLRow> rows, string source)
    {
        var t = new EquityPnLRow { Source = source, Login = 0, Name = "TOTAL" };
        foreach (var r in rows)
        {
            t.BeginEquity        += r.BeginEquity;
            t.NetDepositWithdraw += r.NetDepositWithdraw;
            t.NetCredit          += r.NetCredit;
            t.CommRebate         += r.CommRebate;
            t.SpreadRebate       += r.SpreadRebate;
            t.Adjustment         += r.Adjustment;
            t.ProfitShare        += r.ProfitShare;
            t.SupposedEquity     += r.SupposedEquity;
            t.CurrentEquity      += r.CurrentEquity;
            t.Pl                 += r.Pl;
            t.NetPl              += r.NetPl;
        }
        return t;
    }
}
