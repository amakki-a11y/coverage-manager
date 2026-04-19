namespace CoverageManager.Core.Models.EquityPnL;

/// <summary>
/// One row in the Equity P&amp;L table — either a single login (client or
/// coverage account) or a totals aggregate. All figures are in USD, summed
/// over the requested Beirut-local date range.
/// </summary>
/// <remarks>
/// Column math — kept here so the engine + UI agree:
/// <code>
/// Supposed Eq   = Begin + NetDepW + NetCred
/// PL            = CurrentEq - Supposed Eq
/// NetPL (client) = PL - CommReb - SpreadReb - Adj - PS
/// NetPL (cov)    = PL + CommReb + SpreadReb + Adj + PS
/// </code>
/// Sign convention: rebates / PS on client rows are <b>broker outlays</b>
/// (positive numbers) that the engine strips out of NetPL; the same values on
/// coverage rows are <b>broker income</b> (positive numbers) that stay in
/// NetPL.
/// </remarks>
public class EquityPnLRow
{
    public long Login { get; set; }
    public string Source { get; set; } = "bbook";           // 'bbook' | 'coverage'
    public string Name { get; set; } = string.Empty;        // from trading_accounts.name
    public string Group { get; set; } = string.Empty;       // from trading_accounts.group

    public decimal BeginEquity { get; set; }
    public decimal NetDepositWithdraw { get; set; }
    public decimal NetCredit { get; set; }
    public decimal CommRebate { get; set; }
    public decimal SpreadRebate { get; set; }
    public decimal Adjustment { get; set; }
    public decimal ProfitShare { get; set; }
    public decimal SupposedEquity { get; set; }
    public decimal CurrentEquity { get; set; }
    public decimal Pl { get; set; }
    public decimal NetPl { get; set; }

    /// <summary>True when no <c>account_equity_snapshots</c> row exists before the range start.</summary>
    public bool BeginFromSnapshot { get; set; }

    /// <summary>True when <c>CurrentEquity</c> came from a live <c>trading_accounts</c> read, not a snapshot.</summary>
    public bool CurrentIsLive { get; set; }
}

/// <summary>Full Equity P&amp;L response returned by <c>GET /api/equity-pnl</c>.</summary>
public class EquityPnLResponse
{
    public string From { get; set; } = string.Empty;  // YYYY-MM-DD Beirut
    public string To   { get; set; } = string.Empty;
    public DateTime BeginAnchorUtc { get; set; }
    public DateTime EndAnchorUtc   { get; set; }

    public List<EquityPnLRow> ClientRows   { get; set; } = new();
    public List<EquityPnLRow> CoverageRows { get; set; } = new();

    public EquityPnLRow? ClientsTotal  { get; set; }
    public EquityPnLRow? CoverageTotal { get; set; }

    /// <summary>Broker trading edge = -Clients.NetPL + Coverage.NetPL.</summary>
    public decimal BrokerEdge { get; set; }
}
