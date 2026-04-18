namespace CoverageManager.Core.Models.Bridge;

/// <summary>
/// Source of a single fill as seen by the Centroid Dropcopy FIX 4.4 feed.
/// CLIENT = internal book fill (group matches B-Book pattern).
/// COV_OUT = outbound LP hedge leg (group matches A-Book pattern).
/// UNCLASSIFIED = group didn't match either pattern; treated as CLIENT by default.
/// </summary>
public enum BridgeSource
{
    CLIENT,
    COV_OUT,
    UNCLASSIFIED
}

public enum BridgeSide
{
    BUY,
    SELL
}

/// <summary>
/// A single normalized fill from the Centroid Bridge Dropcopy feed.
/// One Execution Report (FIX MsgType=8) maps to one BridgeDeal.
/// </summary>
public class BridgeDeal
{
    /// <summary>
    /// FIX tag 17 — ExecID. Unique per fill.
    /// </summary>
    public string DealId { get; set; } = string.Empty;

    /// <summary>
    /// FIX tag 37 — Centroid Gateway OrderID.
    /// Multiple BridgeDeals can share this (CLIENT + COV_OUT legs of the same client order).
    /// </summary>
    public string CenOrdId { get; set; } = string.Empty;

    /// <summary>
    /// FIX tag 55 — Symbol. Raw from Centroid, normalization to canonical happens downstream.
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    public BridgeSource Source { get; set; }

    public BridgeSide Side { get; set; }

    /// <summary>
    /// FIX tag 32 — LastQty. In lots.
    /// </summary>
    public decimal Volume { get; set; }

    /// <summary>
    /// FIX tag 31 — LastPx. Symbol's native decimal precision.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// UTC timestamp of the fill. Centroid provides microsecond precision in FIX tags 52/60.
    /// </summary>
    public DateTime TimeUtc { get; set; }

    /// <summary>
    /// FIX tag 90015 — Maker Name (LP or internal B-book server name). Optional.
    /// </summary>
    public string? LpName { get; set; }

    /// <summary>
    /// FIX tag 90016 — Taker Name. Optional, requires explicit enablement on Centroid side.
    /// </summary>
    public string? TakerName { get; set; }

    /// <summary>
    /// FIX tag 90002 — External MT4/MT5 group. Source of CLIENT/COV_OUT classification.
    /// </summary>
    public string? MtGroup { get; set; }

    /// <summary>
    /// FIX tag 90001 — External MT4/MT5 login.
    /// </summary>
    public ulong? MtLogin { get; set; }

    /// <summary>
    /// FIX tag 90003 — External MT4/MT5 ticket.
    /// </summary>
    public ulong? MtTicket { get; set; }

    /// <summary>
    /// ext_dealid — External MT5 deal number (distinct from MtTicket, which is the order id).
    /// </summary>
    public ulong? MtDealId { get; set; }

    /// <summary>
    /// FIX tag 90006 — External position number.
    /// </summary>
    public ulong? PositionId { get; set; }

    /// <summary>
    /// FIX tag 90011 — External markup added by the taker.
    /// </summary>
    public decimal? ExternalMarkup { get; set; }

    /// <summary>
    /// FIX tag 132 — External bid at fill time.
    /// </summary>
    public decimal? BidAtFill { get; set; }

    /// <summary>
    /// FIX tag 133 — External ask at fill time.
    /// </summary>
    public decimal? AskAtFill { get; set; }

    /// <summary>
    /// Canonical symbol after applying B-Book ↔ LP symbol mapping.
    /// Populated by the pairing pipeline, not by FIX parsing.
    /// </summary>
    public string? CanonicalSymbol { get; set; }

    /// <summary>
    /// Centroid's own order number for the LP leg (e.g. "1547100" from maker_order_id_value).
    /// Only populated for COV_OUT deals; null for CLIENT.
    /// </summary>
    public string? MakerOrderId { get; set; }

    /// <summary>raw_avg_price (pre-markup LP VWAP). Only set by maker_orders poller.</summary>
    public decimal? RawPrice { get; set; }
}
