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

    // Hedge ratio: how much of the client net is covered by LP coverage IN THE CLIENT'S DIRECTION
    // (owner decision, V2_PLAN 9.8). Coverage mirrors client direction, so only coverage net on the
    // client's side covers anything; a wrong-way coverage net counts as 0% cover (never negative) and
    // is flagged through WrongWayVolume / IsWrongWay. Measured on nets, like NetVolume / "To Cover":
    // client +10 with coverage BUY 8 + SELL 3 nets to +5 -> 50%. Uncapped above (over-hedge > 100%).
    // No client net -> 100 (nothing to hedge), as before.
    public decimal HedgeRatio => BBookNetVolume == 0 ? 100
        : Math.Max(0m, CoverageNetVolume * Math.Sign(BBookNetVolume)) / Math.Abs(BBookNetVolume) * 100;

    // Lots of coverage net pointing AGAINST the client net (adds to the broker's exposure instead of
    // reducing it). 0 when coverage is same-way, flat, or there is no client net to be against.
    public decimal WrongWayVolume => BBookNetVolume == 0 ? 0
        : Math.Max(0m, -CoverageNetVolume * Math.Sign(BBookNetVolume));

    public bool IsWrongWay => WrongWayVolume > 0;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
