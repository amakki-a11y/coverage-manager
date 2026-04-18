namespace CoverageManager.Core.Models.Bridge;

/// <summary>
/// Client-side markup + book-split detail for one Centroid order (cen_ord_id).
/// Sourced from Centroid's `orders_report` REST endpoint (not available on the
/// live_trades WS). Used to enrich ExecutionPair with real markup numbers.
/// </summary>
public class ClientOrderDetail
{
    public string CenOrdId { get; set; } = string.Empty;

    /// <summary>Requested price at order send time (req_avg_price).</summary>
    public decimal? ReqAvgPrice { get; set; }

    /// <summary>Client-side fill VWAP as reported by Centroid (avg_price).</summary>
    public decimal? AvgPrice { get; set; }

    /// <summary>Total markup charged on this client order (total_markup).</summary>
    public decimal? TotalMarkup { get; set; }

    /// <summary>External taker markup component (ext_markup on orders_report).</summary>
    public decimal? ExtMarkup { get; set; }

    /// <summary>Volume filled on the A-book (LP side).</summary>
    public decimal? AFillVolume { get; set; }

    /// <summary>Volume filled on the B-book (internal).</summary>
    public decimal? BFillVolume { get; set; }

    /// <summary>VWAP of the A-book fills.</summary>
    public decimal? AAvgPrice { get; set; }

    /// <summary>VWAP of the B-book fills.</summary>
    public decimal? BAvgPrice { get; set; }

    /// <summary>When Centroid received this row (recv_time_msc).</summary>
    public DateTime? RecvTimeUtc { get; set; }
}
