using System.Text.Json.Serialization;

namespace CoverageManager.Core.Models;

public class RiskThreshold
{
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? Id { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty; // empty = applies to all symbols

    [JsonPropertyName("trigger_type")]
    public string TriggerType { get; set; } = string.Empty; // "exposure", "hedge_ratio", "pnl", "account_exposure"

    [JsonPropertyName("operator")]
    public string Operator { get; set; } = "gt"; // "gt", "lt", "gte", "lte"

    [JsonPropertyName("value")]
    public decimal Value { get; set; }

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "warning"; // "critical", "warning", "info"

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("created_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? UpdatedAt { get; set; }
}
