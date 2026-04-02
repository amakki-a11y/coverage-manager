using System.Text.Json.Serialization;

namespace CoverageManager.Core.Models;

/// <summary>
/// Audit log entry for any modification to a deal or position.
/// Tracks who changed what, when, and the before/after values.
/// </summary>
public class TradeAuditEntry
{
    [JsonPropertyName("id")]
    public Guid? Id { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = "bbook";

    [JsonPropertyName("deal_id")]
    public long? DealId { get; set; }

    [JsonPropertyName("position_id")]
    public long? PositionId { get; set; }

    [JsonPropertyName("login")]
    public long Login { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("field_changed")]
    public string FieldChanged { get; set; } = string.Empty;

    [JsonPropertyName("old_value")]
    public string? OldValue { get; set; }

    [JsonPropertyName("new_value")]
    public string? NewValue { get; set; }

    [JsonPropertyName("changed_by")]
    public string ChangedBy { get; set; } = string.Empty;

    [JsonPropertyName("change_type")]
    public string ChangeType { get; set; } = "modified";

    [JsonPropertyName("detected_at")]
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}
