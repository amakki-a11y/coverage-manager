using System.Text.Json.Serialization;

namespace CoverageManager.Core.Models.EquityPnL;

/// <summary>
/// Point-in-time equity snapshot for a single trading account (login).
/// Rows come from the same scheduler that feeds <see cref="ExposureSnapshot"/>;
/// they're the source of truth for both the period's Begin Equity and the
/// monthly P&amp;L slices consumed by <c>PsHighWaterMarkEngine</c>.
/// </summary>
public class AccountEquitySnapshot
{
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? Id { get; set; }

    [JsonPropertyName("login")]
    public long Login { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = "bbook";

    [JsonPropertyName("snapshot_time")]
    public DateTime SnapshotTime { get; set; }

    [JsonPropertyName("balance")]
    public decimal Balance { get; set; }

    [JsonPropertyName("equity")]
    public decimal Equity { get; set; }

    [JsonPropertyName("credit")]
    public decimal Credit { get; set; }

    [JsonPropertyName("margin")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Margin { get; set; }

    [JsonPropertyName("trigger_type")]
    public string TriggerType { get; set; } = "scheduled";

    [JsonPropertyName("label")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; set; }
}
