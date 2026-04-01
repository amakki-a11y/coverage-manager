namespace CoverageManager.Core.Models;

/// <summary>
/// Realized P&L summary for a single day.
/// </summary>
public class DailyPnL
{
    public DateTime Date { get; set; }
    public int DealCount { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal TotalCommission { get; set; }
    public decimal TotalSwap { get; set; }
    public decimal TotalFee { get; set; }
    public decimal NetPnL => TotalProfit + TotalCommission + TotalSwap + TotalFee;
    public List<SymbolPnL> Symbols { get; set; } = [];
}
