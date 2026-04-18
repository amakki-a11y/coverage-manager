using System.Text.Json.Serialization;

namespace CoverageManager.Core.Models;

/// <summary>
/// Point-in-time snapshot of B-Book + Coverage floating P&L per canonical symbol.
/// Used by the Period P&L feature to anchor "Begin" balances for a date range.
/// Maps to the `exposure_snapshots` Supabase table.
/// </summary>
public class ExposureSnapshot
{
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? Id { get; set; }

    [JsonPropertyName("canonical_symbol")]
    public string CanonicalSymbol { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_time")]
    public DateTime SnapshotTime { get; set; }

    [JsonPropertyName("bbook_buy_volume")]  public decimal BBookBuyVolume  { get; set; }
    [JsonPropertyName("bbook_sell_volume")] public decimal BBookSellVolume { get; set; }

    [JsonPropertyName("coverage_buy_volume")]  public decimal CoverageBuyVolume  { get; set; }
    [JsonPropertyName("coverage_sell_volume")] public decimal CoverageSellVolume { get; set; }

    [JsonPropertyName("net_volume")] public decimal NetVolume { get; set; }

    [JsonPropertyName("bbook_pnl")]    public decimal BBookPnL    { get; set; }
    [JsonPropertyName("coverage_pnl")] public decimal CoveragePnL { get; set; }
    [JsonPropertyName("net_pnl")]      public decimal NetPnL      { get; set; }

    /// <summary>How this snapshot was captured: scheduled | manual | daily | weekly | monthly.</summary>
    [JsonPropertyName("trigger_type")] public string TriggerType { get; set; } = "scheduled";

    /// <summary>Optional free-text label from a manual capture (e.g. "Before FOMC").</summary>
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
}
