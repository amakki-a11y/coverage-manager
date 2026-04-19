using System.Text.Json.Serialization;

namespace CoverageManager.Core.Models.EquityPnL;

/// <summary>
/// Per-login per-canonical-symbol spread rebate rate. Looked up by
/// <c>EquityPnLEngine</c> for every trade deal in the window — any trade
/// with no matching row contributes 0 to the Spread Rebate bucket.
/// </summary>
public class SpreadRebateRate
{
    [JsonPropertyName("login")]
    public long Login { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = "bbook";

    [JsonPropertyName("canonical_symbol")]
    public string CanonicalSymbol { get; set; } = string.Empty;

    /// <summary>Rebate in USD per standard lot, e.g. 5.0 = "$5 per lot".</summary>
    [JsonPropertyName("rate_per_lot")]
    public decimal RatePerLot { get; set; }

    [JsonPropertyName("updated_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? UpdatedAt { get; set; }
}
