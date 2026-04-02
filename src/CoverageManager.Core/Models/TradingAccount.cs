using System.Text.Json.Serialization;

namespace CoverageManager.Core.Models;

/// <summary>
/// Mirror of an MT5 trading account (B-Book client or Coverage LP).
/// Synced to Supabase for persistent tracking.
/// </summary>
public class TradingAccount
{
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? Id { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = "bbook"; // "bbook" or "coverage"

    [JsonPropertyName("login")]
    public long Login { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("group_name")]
    public string GroupName { get; set; } = string.Empty;

    [JsonPropertyName("leverage")]
    public int Leverage { get; set; }

    [JsonPropertyName("balance")]
    public decimal Balance { get; set; }

    [JsonPropertyName("equity")]
    public decimal Equity { get; set; }

    [JsonPropertyName("margin")]
    public decimal Margin { get; set; }

    [JsonPropertyName("free_margin")]
    public decimal FreeMargin { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "USD";

    [JsonPropertyName("registration_time")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? RegistrationTime { get; set; }

    [JsonPropertyName("last_trade_time")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? LastTradeTime { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";

    [JsonPropertyName("comment")]
    public string Comment { get; set; } = string.Empty;

    [JsonPropertyName("synced_at")]
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("created_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
