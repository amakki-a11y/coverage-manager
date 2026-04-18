namespace CoverageManager.Core.Models;

/// <summary>
/// DTO received from Python collector via HTTP POST.
/// </summary>
public class CoveragePositionDto
{
    public string Symbol { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty; // "BUY" or "SELL"
    public decimal Volume { get; set; }
    public decimal OpenPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal Profit { get; set; }
    public decimal Swap { get; set; }
    public long Ticket { get; set; } // position ticket for dedup
    public long Login { get; set; } // Coverage account login (constant per collector session)
    public DateTime? OpenTime { get; set; } // UTC — null until collector is upgraded to send it
}
