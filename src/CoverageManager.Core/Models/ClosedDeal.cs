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
}
