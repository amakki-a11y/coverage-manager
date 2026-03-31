using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;

namespace CoverageManager.Tests;

[TestClass]
public class PositionManagerTests
{
    private PositionManager _pm = null!;

    [TestInitialize]
    public void Setup()
    {
        _pm = new PositionManager();
        _pm.LoadMappings(new[]
        {
            new SymbolMapping
            {
                CanonicalName = "XAUUSD",
                BBookSymbol = "XAUUSD",
                BBookContractSize = 100,
                CoverageSymbol = "GOLD",
                CoverageContractSize = 1,
                IsActive = true
            },
            new SymbolMapping
            {
                CanonicalName = "EURUSD",
                BBookSymbol = "EURUSD",
                BBookContractSize = 100000,
                CoverageSymbol = "EURUSD.r",
                CoverageContractSize = 100000,
                IsActive = true
            }
        });
    }

    [TestMethod]
    public void UpdateBBookPosition_WithMapping_SetsCanonicalSymbol()
    {
        var pos = new Position { Source = "bbook", Symbol = "XAUUSD", VolumeLots = 5 };
        _pm.UpdateBBookPosition("bbook:1001:100", pos);

        var positions = _pm.GetBBookPositions();
        Assert.AreEqual(1, positions.Count);
        Assert.AreEqual("XAUUSD", positions[0].CanonicalSymbol);
        Assert.AreEqual(5m, positions[0].VolumeNormalized);
    }

    [TestMethod]
    public void UpdateBBookPosition_WithoutMapping_UsesRawSymbol()
    {
        var pos = new Position { Source = "bbook", Symbol = "UNKNOWN", VolumeLots = 3 };
        _pm.UpdateBBookPosition("bbook:1001:101", pos);

        var positions = _pm.GetBBookPositions();
        Assert.AreEqual("UNKNOWN", positions[0].CanonicalSymbol);
    }

    [TestMethod]
    public void RemoveBBookPosition_RemovesCorrectly()
    {
        var pos = new Position { Source = "bbook", Symbol = "EURUSD", VolumeLots = 1 };
        _pm.UpdateBBookPosition("bbook:1001:100", pos);
        Assert.AreEqual(1, _pm.GetBBookPositions().Count);

        _pm.RemoveBBookPosition("bbook:1001:100");
        Assert.AreEqual(0, _pm.GetBBookPositions().Count);
    }

    [TestMethod]
    public void UpdateCoveragePositions_NormalizesVolume()
    {
        var dtos = new[]
        {
            new CoveragePositionDto
            {
                Symbol = "GOLD",
                Direction = "BUY",
                Volume = 1500,
                OpenPrice = 2650,
                CurrentPrice = 2655,
                Profit = 7500,
                Ticket = 200
            }
        };

        _pm.UpdateCoveragePositions(dtos);
        var positions = _pm.GetCoveragePositions();

        Assert.AreEqual(1, positions.Count);
        Assert.AreEqual("XAUUSD", positions[0].CanonicalSymbol);
        Assert.AreEqual(15m, positions[0].VolumeNormalized); // 1500 × (1/100)
    }

    [TestMethod]
    public void UpdateCoveragePositions_ReplacesOldPositions()
    {
        var first = new[] { new CoveragePositionDto { Symbol = "GOLD", Direction = "BUY", Volume = 100, Ticket = 1 } };
        _pm.UpdateCoveragePositions(first);
        Assert.AreEqual(1, _pm.GetCoveragePositions().Count);

        var second = new[]
        {
            new CoveragePositionDto { Symbol = "GOLD", Direction = "SELL", Volume = 200, Ticket = 2 },
            new CoveragePositionDto { Symbol = "EURUSD.r", Direction = "BUY", Volume = 5, Ticket = 3 }
        };
        _pm.UpdateCoveragePositions(second);
        Assert.AreEqual(2, _pm.GetCoveragePositions().Count);
    }

    [TestMethod]
    public void GetAllPositions_CombinesBothSources()
    {
        _pm.UpdateBBookPosition("bbook:1001:100", new Position { Source = "bbook", Symbol = "XAUUSD", VolumeLots = 5 });
        _pm.UpdateCoveragePositions(new[] { new CoveragePositionDto { Symbol = "GOLD", Direction = "BUY", Volume = 100, Ticket = 1 } });

        Assert.AreEqual(2, _pm.GetAllPositions().Count);
    }

    [TestMethod]
    public void FindMapping_CaseInsensitive()
    {
        var mapping = _pm.FindMapping("xauusd", "bbook");
        Assert.IsNotNull(mapping);
        Assert.AreEqual("XAUUSD", mapping.CanonicalName);
    }
}
