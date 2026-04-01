namespace CoverageManager.Core.Models;

/// <summary>
/// Realized P&L summary for a single symbol.
/// </summary>
public class SymbolPnL
{
    public string Symbol { get; set; } = string.Empty;
    public int DealCount { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal TotalCommission { get; set; }
    public decimal TotalSwap { get; set; }
    public decimal TotalFee { get; set; }
    public decimal NetPnL => TotalProfit + TotalCommission + TotalSwap + TotalFee;
    public decimal TotalVolume { get; set; }
    public decimal BuyVolume { get; set; }
    public decimal SellVolume { get; set; }
}
