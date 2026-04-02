using System.Text.Json.Serialization;

namespace CoverageManager.Core.Models;

/// <summary>
/// Deal record for Supabase persistence.
/// Maps to the `deals` table.
/// </summary>
public class DealRecord
{
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? Id { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = "bbook";

    [JsonPropertyName("deal_id")]
    public long DealId { get; set; }

    [JsonPropertyName("login")]
    public long Login { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("canonical_symbol")]
    public string CanonicalSymbol { get; set; } = string.Empty;

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public int Action { get; set; } // 0=BUY, 1=SELL

    [JsonPropertyName("entry")]
    public int Entry { get; set; } // 0=IN, 1=OUT, 2=INOUT, 3=OUT_BY

    [JsonPropertyName("volume")]
    public decimal Volume { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("profit")]
    public decimal Profit { get; set; }

    [JsonPropertyName("commission")]
    public decimal Commission { get; set; }

    [JsonPropertyName("swap")]
    public decimal Swap { get; set; }

    [JsonPropertyName("fee")]
    public decimal Fee { get; set; }

    [JsonPropertyName("position_id")]
    public long? PositionId { get; set; }

    [JsonPropertyName("deal_time")]
    public DateTime DealTime { get; set; }

    [JsonPropertyName("created_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? CreatedAt { get; set; }
}
