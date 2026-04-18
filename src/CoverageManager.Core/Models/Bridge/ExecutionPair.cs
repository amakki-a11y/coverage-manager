namespace CoverageManager.Core.Models.Bridge;

/// <summary>
/// One CLIENT fill paired with its attributed COV_OUT fills.
/// Mirrors the bridge_executions row shape in Supabase.
/// </summary>
public class ExecutionPair
{
    /// <summary>
    /// FIX tag 17 (ExecID) of the CLIENT fill. Primary key — unique per row.
    /// </summary>
    public string ClientDealId { get; set; } = string.Empty;

    /// <summary>
    /// Canonical symbol (post symbol-mapping). e.g. "XAUUSD".
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    public BridgeSide Side { get; set; }

    public decimal ClientVolume { get; set; }
    public decimal ClientPrice { get; set; }
    public DateTime ClientTimeUtc { get; set; }

    /// <summary>
    /// Centroid OrderID from FIX tag 37 — groups the CLIENT fill with its COV_OUT legs.
    /// </summary>
    public string CenOrdId { get; set; } = string.Empty;

    /// <summary>
    /// MT5 ticket (ext_order) for the client fill. Null when the feed didn't expose one.
    /// </summary>
    public ulong? ClientMtTicket { get; set; }

    /// <summary>
    /// MT5 deal number (ext_dealid) for the client fill. Null when the feed didn't expose one.
    /// </summary>
    public ulong? ClientMtDealId { get; set; }

    /// <summary>
    /// MT5 login of the client account that placed the trade (ext_login in Centroid).
    /// </summary>
    public ulong? ClientMtLogin { get; set; }

    /// <summary>
    /// Attributed coverage legs, sorted by absolute |TimeDiffMs| ascending.
    /// </summary>
    public List<CovFill> CovFills { get; set; } = new();

    // ------ Derived metrics (populated by BridgePairingEngine) ------

    public decimal CovVolume { get; set; }

    /// <summary>
    /// Σ(volume × price) / Σ(volume) across CovFills. 0 when no fills.
    /// </summary>
    public decimal AvgCovPrice { get; set; }

    /// <summary>
    /// CovVolume / ClientVolume.
    /// </summary>
    public decimal CoverageRatio { get; set; }

    /// <summary>
    /// Signed per-unit price edge, positive = broker gain.
    /// SELL client: AvgCovPrice - ClientPrice.
    /// BUY  client: ClientPrice - AvgCovPrice.
    /// </summary>
    public decimal PriceEdge { get; set; }

    /// <summary>
    /// PriceEdge converted to pips using the symbol's pipSize.
    /// </summary>
    public decimal Pips { get; set; }

    /// <summary>
    /// Largest (most positive) time diff in the CovFills set.
    /// </summary>
    public int MaxTimeDiffMs { get; set; }

    /// <summary>
    /// Smallest (most negative) time diff in the CovFills set.
    /// </summary>
    public int MinTimeDiffMs { get; set; }

    /// <summary>
    /// When the row was computed. Used to indicate freshness in the UI.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // ------ Client-side markup detail (orders_report enrichment) ------

    /// <summary>Requested price at order send time (req_avg_price).</summary>
    public decimal? ClientReqPrice { get; set; }

    /// <summary>Total markup on the client order (total_markup). Non-null once enriched.</summary>
    public decimal? ClientTotalMarkup { get; set; }

    /// <summary>External-taker markup component from orders_report.ext_markup.</summary>
    public decimal? ClientExtMarkup { get; set; }

    /// <summary>Volume routed to the A-book (LP).</summary>
    public decimal? ClientAFillVolume { get; set; }

    /// <summary>Volume filled on the B-book (internal).</summary>
    public decimal? ClientBFillVolume { get; set; }

    /// <summary>A-book VWAP.</summary>
    public decimal? ClientAAvgPrice { get; set; }

    /// <summary>B-book VWAP.</summary>
    public decimal? ClientBAvgPrice { get; set; }
}
