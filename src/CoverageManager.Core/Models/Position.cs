namespace CoverageManager.Core.Models;

public class Position
{
    public string Source { get; set; } = string.Empty; // "bbook" or "coverage"
    public ulong Login { get; set; }
    public string Symbol { get; set; } = string.Empty; // raw symbol from source
    public string CanonicalSymbol { get; set; } = string.Empty; // mapped standard name
    public string Direction { get; set; } = string.Empty; // "BUY" or "SELL"
    public decimal VolumeLots { get; set; } // raw lots from source
    public decimal VolumeNormalized { get; set; } // converted to B-Book lot equivalent
    public decimal OpenPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal Profit { get; set; }
    public decimal Swap { get; set; }
    public DateTime OpenTime { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
