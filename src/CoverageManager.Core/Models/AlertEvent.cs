using System.Text.Json.Serialization;

namespace CoverageManager.Core.Models;

public class AlertEvent
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("threshold_id")]
    public Guid ThresholdId { get; set; }

    [JsonPropertyName("trigger_type")]
    public string TriggerType { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "warning";

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("threshold_value")]
    public decimal ThresholdValue { get; set; }

    [JsonPropertyName("actual_value")]
    public decimal ActualValue { get; set; }

    [JsonPropertyName("triggered_at")]
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("acknowledged")]
    public bool Acknowledged { get; set; } = false;

    [JsonPropertyName("acknowledged_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? AcknowledgedAt { get; set; }
}
