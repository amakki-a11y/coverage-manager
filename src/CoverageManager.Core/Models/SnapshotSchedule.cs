using System.Text.Json.Serialization;

namespace CoverageManager.Core.Models;

/// <summary>
/// A dealer-configured cadence for capturing exposure snapshots.
/// Maps to the `snapshot_schedules` Supabase table.
/// </summary>
public class SnapshotSchedule
{
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? Id { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    /// <summary>daily | weekly | monthly | custom</summary>
    [JsonPropertyName("cadence")] public string Cadence { get; set; } = "daily";

    /// <summary>
    /// Cron expression. Populated automatically for daily/weekly/monthly;
    /// dealer-supplied for custom.
    /// </summary>
    [JsonPropertyName("cron_expr")] public string? CronExpr { get; set; }

    /// <summary>IANA timezone (default Asia/Beirut).</summary>
    [JsonPropertyName("tz")] public string Tz { get; set; } = "Asia/Beirut";

    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;

    [JsonPropertyName("last_run_at")] public DateTime? LastRunAt { get; set; }
    [JsonPropertyName("next_run_at")] public DateTime? NextRunAt { get; set; }

    [JsonPropertyName("created_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? UpdatedAt { get; set; }
}
