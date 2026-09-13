using System.Text.Json.Serialization;

namespace CoverageManager.Core.Models;

/// <summary>One run of the nightly 12-month deal-retention pruner (table retention_prune_runs).</summary>
public class RetentionPruneRun
{
    [JsonPropertyName("id")] public Guid? Id { get; set; }
    [JsonPropertyName("trigger_type")] public string TriggerType { get; set; } = "scheduled";
    [JsonPropertyName("retention_months")] public int RetentionMonths { get; set; }
    [JsonPropertyName("cutoff_utc")] public DateTime CutoffUtc { get; set; }
    [JsonPropertyName("started_at")] public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    [JsonPropertyName("finished_at")] public DateTime? FinishedAt { get; set; }
    [JsonPropertyName("deleted")] public long Deleted { get; set; }
    [JsonPropertyName("batches")] public int Batches { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("notes")] public string Notes { get; set; } = string.Empty;
}
