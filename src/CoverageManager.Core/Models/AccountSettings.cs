using System.Text.Json.Serialization;

namespace CoverageManager.Core.Models;

public class AccountSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("account_type")]
    public string AccountType { get; set; } = string.Empty; // "manager" or "coverage"

    public string Label { get; set; } = string.Empty;

    public string Server { get; set; } = string.Empty;

    public long Login { get; set; }

    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("group_mask")]
    public string GroupMask { get; set; } = "*";

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
