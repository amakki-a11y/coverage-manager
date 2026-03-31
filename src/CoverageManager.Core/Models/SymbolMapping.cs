namespace CoverageManager.Core.Models;

public class SymbolMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CanonicalName { get; set; } = string.Empty;
    public string BBookSymbol { get; set; } = string.Empty;
    public decimal BBookContractSize { get; set; }
    public string CoverageSymbol { get; set; } = string.Empty;
    public decimal CoverageContractSize { get; set; }
    public int Digits { get; set; }
    public string ProfitCurrency { get; set; } = "USD";
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
