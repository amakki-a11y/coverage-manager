using System.Text.Json.Serialization;

namespace CoverageManager.Core.Models;

/// <summary>
/// A single run of the deal-reconciliation sweep. One row per invocation, whether
/// scheduled or triggered manually from the Settings tab. Maps to the
/// `reconciliation_runs` Supabase table.
/// </summary>
public class ReconciliationRun
{
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? Id { get; set; }

    [JsonPropertyName("trigger_type")] public string TriggerType { get; set; } = "scheduled";

    [JsonPropertyName("window_from")] public DateTime WindowFrom { get; set; }
    [JsonPropertyName("window_to")]   public DateTime WindowTo   { get; set; }

    [JsonPropertyName("started_at")]  public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("finished_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? FinishedAt { get; set; }

    [JsonPropertyName("mt5_deal_count")]      public long Mt5DealCount      { get; set; }
    [JsonPropertyName("supabase_deal_count")] public long SupabaseDealCount { get; set; }
    [JsonPropertyName("backfilled")]          public long Backfilled        { get; set; }
    [JsonPropertyName("ghost_deleted")]       public long GhostDeleted      { get; set; }
    [JsonPropertyName("modified")]            public long Modified          { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }

    [JsonPropertyName("notes")] public string Notes { get; set; } = string.Empty;
}
