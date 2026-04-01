namespace CoverageManager.Core.Models;

public class ExposureSummary
{
    public string CanonicalSymbol { get; set; } = string.Empty;

    // B-Book side (your clients)
    public decimal BBookBuyVolume { get; set; }
    public decimal BBookBuyAvgPrice { get; set; }
    public decimal BBookSellVolume { get; set; }
    public decimal BBookSellAvgPrice { get; set; }
    public decimal BBookNetVolume => BBookBuyVolume - BBookSellVolume;
    public decimal BBookPnL { get; set; }

    // Coverage side (LP hedge) — already normalized to B-Book lots
    public decimal CoverageBuyVolume { get; set; }
    public decimal CoverageBuyAvgPrice { get; set; }
    public decimal CoverageSellVolume { get; set; }
    public decimal CoverageSellAvgPrice { get; set; }
    public decimal CoverageNetVolume => CoverageBuyVolume - CoverageSellVolume;
    public decimal CoveragePnL { get; set; }

    // Net exposure: remaining uncovered volume (BBook - Coverage, since coverage mirrors client direction)
    public decimal NetVolume => BBookNetVolume - CoverageNetVolume;
    public decimal NetPnL => -BBookPnL + CoveragePnL;

    // Hedge ratio: how much of B-Book is covered by LP
    public decimal HedgeRatio => BBookNetVolume == 0 ? 100
        : Math.Min(100, Math.Abs(CoverageNetVolume / BBookNetVolume) * 100);

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
