using System.Text.Json.Serialization;

namespace CoverageManager.Core.Models.EquityPnL;

/// <summary>
/// A named group of logins used for Equity P&amp;L rebate / profit-share
/// configuration at scale. Dealer creates groups like "IB-Lebanon" or
/// "VIP-TierA", assigns logins to them, and the endpoint resolves
/// per-login rates as: login-specific override → group config → 0.
/// </summary>
public class LoginGroup
{
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("created_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Many-to-many row linking a login to a group. `priority` breaks ties when a
/// login belongs to multiple groups — highest priority wins.
/// </summary>
public class LoginGroupMember
{
    [JsonPropertyName("group_id")]
    public Guid GroupId { get; set; }

    [JsonPropertyName("login")]
    public long Login { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = "bbook";

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("added_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? AddedAt { get; set; }
}

/// <summary>
/// Rebate / PS configuration shared by every login in a group. Login-specific
/// <see cref="EquityPnLClientConfig"/> rows override this when both exist.
/// </summary>
public class EquityPnLGroupConfig
{
    [JsonPropertyName("group_id")]
    public Guid GroupId { get; set; }

    [JsonPropertyName("comm_rebate_pct")]
    public decimal CommRebatePct { get; set; }

    [JsonPropertyName("ps_pct")]
    public decimal PsPct { get; set; }

    [JsonPropertyName("notes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Notes { get; set; }

    [JsonPropertyName("updated_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Per-group per-canonical-symbol spread rebate rate.</summary>
public class GroupSpreadRebateRate
{
    [JsonPropertyName("group_id")]
    public Guid GroupId { get; set; }

    [JsonPropertyName("canonical_symbol")]
    public string CanonicalSymbol { get; set; } = string.Empty;

    [JsonPropertyName("rate_per_lot")]
    public decimal RatePerLot { get; set; }

    [JsonPropertyName("updated_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? UpdatedAt { get; set; }
}
