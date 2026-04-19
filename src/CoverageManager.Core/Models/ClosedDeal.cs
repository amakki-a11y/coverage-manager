namespace CoverageManager.Core.Models;

/// <summary>
/// A closed deal from the MT5 Manager API.
/// Used to track realized P&L per symbol.
/// </summary>
public class ClosedDeal
{
    public ulong DealId { get; set; }
    public ulong Login { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty; // "BUY" or "SELL"
    public decimal VolumeLots { get; set; }
    public decimal Price { get; set; }
    public decimal Profit { get; set; }
    public decimal Commission { get; set; }
    public decimal Swap { get; set; }
    public decimal Fee { get; set; }
    public uint Entry { get; set; } // 0=IN, 1=OUT, 2=INOUT, 3=OUT_BY
    public ulong OrderId { get; set; } // MTDeal.Order — ticket of the order that created this deal
    public ulong PositionId { get; set; } // MTDeal.PositionID
    public DateTime Time { get; set; }

    /// <summary>
    /// Raw MT5 <c>DealAction</c> code. Preserved for downstream classification:
    ///   0 = BUY, 1 = SELL  (trade deals — filtered into DealStore)
    ///   2 = BALANCE        (deposits/withdrawals)
    ///   3 = CREDIT
    ///   4 = CHARGE
    ///   5 = CORRECTION
    ///   6 = BONUS
    ///   7 = COMMISSION
    /// EquityPnLEngine uses this to bucket deals into the Comm Reb / Spread
    /// Reb / Adj / Net Dep / Net Cred columns.
    /// </summary>
    public uint Action { get; set; }
}
