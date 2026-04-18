namespace CoverageManager.Core.Models.Bridge;

/// <summary>
/// One coverage (COV_OUT) leg attributed to a CLIENT deal inside an ExecutionPair.
/// Serialized into the cov_fills jsonb column on bridge_executions.
/// </summary>
public class CovFill
{
    public string DealId { get; set; } = string.Empty;
    public decimal Volume { get; set; }
    public decimal Price { get; set; }
    public DateTime TimeUtc { get; set; }

    /// <summary>
    /// Signed difference cov.time - client.time in milliseconds.
    /// Negative => coverage executed before client (pre-hedge).
    /// </summary>
    public int TimeDiffMs { get; set; }

    /// <summary>
    /// Maker Name (FIX tag 90015) — which LP filled this leg. Optional.
    /// </summary>
    public string? LpName { get; set; }

    /// <summary>
    /// MT5 ticket for this coverage leg (ext_order in Centroid's orders_report).
    /// Lets an operator cross-reference to the MT5 Manager account view.
    /// </summary>
    public ulong? MtTicket { get; set; }

    /// <summary>
    /// MT5 deal number for this coverage leg (ext_dealid in Centroid).
    /// </summary>
    public ulong? MtDealId { get; set; }

    /// <summary>
    /// Centroid's LP-side order ID (maker_order_id_value). Useful for LP dispute lookups.
    /// </summary>
    public string? MakerOrderId { get; set; }

    /// <summary>
    /// Centroid raw_avg_price — LP fill VWAP BEFORE any Centroid-layer markup.
    /// If this differs from Price (avg_price), the delta is Centroid's own markup.
    /// </summary>
    public decimal? RawPrice { get; set; }

    /// <summary>
    /// ext_markup from Centroid — explicit markup the taker charged on this leg (may be 0).
    /// </summary>
    public decimal? ExtMarkup { get; set; }
}
