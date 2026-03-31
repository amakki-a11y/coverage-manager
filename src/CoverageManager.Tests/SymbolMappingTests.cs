using CoverageManager.Core.Models;

namespace CoverageManager.Tests;

[TestClass]
public class SymbolMappingTests
{
    [TestMethod]
    public void NormalizeCoverageVolume_GoldDifferentContractSizes_ConvertsCorrectly()
    {
        // XAUUSD: B-Book=100oz/lot, Coverage=1oz/lot
        var mapping = new SymbolMapping
        {
            BBookContractSize = 100,
            CoverageContractSize = 1
        };

        // 1500 coverage lots × (1/100) = 15 B-Book lots
        var result = mapping.NormalizeCoverageVolume(1500);
        Assert.AreEqual(15m, result);
    }

    [TestMethod]
    public void NormalizeCoverageVolume_SameContractSize_ReturnsUnchanged()
    {
        var mapping = new SymbolMapping
        {
            BBookContractSize = 100000,
            CoverageContractSize = 100000
        };

        var result = mapping.NormalizeCoverageVolume(5.0m);
        Assert.AreEqual(5.0m, result);
    }

    [TestMethod]
    public void NormalizeCoverageVolume_ZeroBBookContractSize_ReturnsSameVolume()
    {
        var mapping = new SymbolMapping
        {
            BBookContractSize = 0,
            CoverageContractSize = 1
        };

        var result = mapping.NormalizeCoverageVolume(10m);
        Assert.AreEqual(10m, result);
    }

    [TestMethod]
    public void ConvertToCoverageLots_GoldDifferentContractSizes_ConvertsCorrectly()
    {
        var mapping = new SymbolMapping
        {
            BBookContractSize = 100,
            CoverageContractSize = 1
        };

        // 5 B-Book lots × (100/1) = 500 coverage lots
        var result = mapping.ConvertToCoverageLots(5);
        Assert.AreEqual(500m, result);
    }

    [TestMethod]
    public void ConvertToCoverageLots_SameContractSize_ReturnsUnchanged()
    {
        var mapping = new SymbolMapping
        {
            BBookContractSize = 100000,
            CoverageContractSize = 100000
        };

        var result = mapping.ConvertToCoverageLots(2.5m);
        Assert.AreEqual(2.5m, result);
    }

    [TestMethod]
    public void ConvertToCoverageLots_ZeroCoverageContractSize_ReturnsSameVolume()
    {
        var mapping = new SymbolMapping
        {
            BBookContractSize = 100,
            CoverageContractSize = 0
        };

        var result = mapping.ConvertToCoverageLots(5m);
        Assert.AreEqual(5m, result);
    }

    [TestMethod]
    public void NormalizeAndConvert_AreInverse()
    {
        var mapping = new SymbolMapping
        {
            BBookContractSize = 100,
            CoverageContractSize = 1
        };

        var bbookLots = 10m;
        var coverageLots = mapping.ConvertToCoverageLots(bbookLots);
        var backToBBook = mapping.NormalizeCoverageVolume(coverageLots);

        Assert.AreEqual(bbookLots, backToBBook);
    }
}
