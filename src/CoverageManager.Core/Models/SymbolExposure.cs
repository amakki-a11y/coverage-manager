namespace CoverageManager.Core.Models;

public class SymbolExposure
{
    public string Symbol { get; set; } = string.Empty;
    public decimal ClientBuyVolume { get; set; }
    public decimal ClientSellVolume { get; set; }
    public decimal ClientNetVolume { get; set; }
    public decimal ClientPnl { get; set; }
    public decimal ClientAvgEntryPrice { get; set; }
    public decimal ClientAvgExitPrice { get; set; }
    public int ClientTradeCount { get; set; }
    public int ClientWins { get; set; }

    public decimal CoverageBuyVolume { get; set; }
    public decimal CoverageSellVolume { get; set; }
    public decimal CoverageNetVolume { get; set; }
    public decimal CoveragePnl { get; set; }
    public decimal CoverageAvgEntryPrice { get; set; }
    public decimal CoverageAvgExitPrice { get; set; }
    public int CoverageTradeCount { get; set; }
    public int CoverageWins { get; set; }

    public decimal NetExposure { get; set; }
    public decimal HedgePercent { get; set; }
    public decimal EntryPriceDelta { get; set; }
    public decimal ExitPriceDelta { get; set; }
    public decimal NetPnl { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
