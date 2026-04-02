namespace CoverageManager.Core.Models;

public class TradeRecord
{
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;       // "client" | "coverage"
    public string Direction { get; set; } = string.Empty;   // "BUY" | "SELL"
    public decimal Volume { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public DateTime EntryTime { get; set; }
    public DateTime ExitTime { get; set; }
    public decimal Profit { get; set; }
}
