namespace CoverageManager.Core.Models;

public class PriceQuote
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Bid { get; set; }
    public decimal Ask { get; set; }
    public decimal Spread => Ask - Bid;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
