using System.Text.Json.Serialization;

namespace CoverageManager.Core.Models;

public class SymbolMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("canonical_name")]
    public string CanonicalName { get; set; } = string.Empty;

    [JsonPropertyName("bbook_symbol")]
    public string BBookSymbol { get; set; } = string.Empty;

    [JsonPropertyName("bbook_contract_size")]
    public decimal BBookContractSize { get; set; }

    [JsonPropertyName("coverage_symbol")]
    public string CoverageSymbol { get; set; } = string.Empty;

    [JsonPropertyName("coverage_contract_size")]
    public decimal CoverageContractSize { get; set; }

    public int Digits { get; set; }

    [JsonPropertyName("profit_currency")]
    public string ProfitCurrency { get; set; } = "USD";

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Convert coverage lots to B-Book equivalent lots.
    /// E.g., 1500 lots × (1 oz / 100 oz) = 15 B-Book lots
    /// </summary>
    public decimal NormalizeCoverageVolume(decimal coverageLots)
    {
        if (BBookContractSize == 0) return coverageLots;
        return coverageLots * CoverageContractSize / BBookContractSize;
    }

    /// <summary>
    /// Convert B-Book lots to coverage lots for order execution.
    /// E.g., 5 B-Book lots × (100 oz / 1 oz) = 500 coverage lots
    /// </summary>
    public decimal ConvertToCoverageLots(decimal bbookLots)
    {
        if (CoverageContractSize == 0) return bbookLots;
        return bbookLots * BBookContractSize / CoverageContractSize;
    }
}
