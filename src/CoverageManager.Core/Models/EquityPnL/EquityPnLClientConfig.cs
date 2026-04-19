using System.Text.Json.Serialization;

namespace CoverageManager.Core.Models.EquityPnL;

/// <summary>
/// Per-login configuration that drives the commission-rebate and profit-share
/// computations. The PS columns (<c>PsCumPl</c>, <c>PsLowWaterMark</c>,
/// <c>PsLastProcessedMonth</c>) are running state written back by
/// <c>PsHighWaterMarkEngine</c> as months advance — dealers shouldn't edit
/// them by hand.
/// </summary>
public class EquityPnLClientConfig
{
    [JsonPropertyName("login")]
    public long Login { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = "bbook";

    /// <summary>Commission rebate percentage (0..100). 50 = "client gets 50% of paid commission back".</summary>
    [JsonPropertyName("comm_rebate_pct")]
    public decimal CommRebatePct { get; set; }

    /// <summary>Profit-share (loss-share) percentage (0..100). 10 = "10% of new drawdown".</summary>
    [JsonPropertyName("ps_pct")]
    public decimal PsPct { get; set; }

    /// <summary>Date the PS contract started. Null = PS disabled for this login.</summary>
    [JsonPropertyName("ps_contract_start")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? PsContractStart { get; set; }

    /// <summary>Running cumulative trading P&amp;L since contract start. Engine-managed.</summary>
    [JsonPropertyName("ps_cum_pl")]
    public decimal PsCumPl { get; set; }

    /// <summary>Most-negative cum_pl ever seen — new PS triggers below this. Engine-managed.</summary>
    [JsonPropertyName("ps_low_water_mark")]
    public decimal PsLowWaterMark { get; set; }

    /// <summary>Last month-end processed by the HWM engine. Prevents double-counting on reruns.</summary>
    [JsonPropertyName("ps_last_processed_month")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? PsLastProcessedMonth { get; set; }

    [JsonPropertyName("notes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Notes { get; set; }

    [JsonPropertyName("updated_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? UpdatedAt { get; set; }
}
