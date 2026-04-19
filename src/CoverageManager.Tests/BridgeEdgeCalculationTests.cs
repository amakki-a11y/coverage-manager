using CoverageManager.Core.Engines;
using CoverageManager.Core.Models.Bridge;

namespace CoverageManager.Tests;

[TestClass]
public class BridgeEdgeCalculationTests
{
    private static readonly DateTime T0 = new(2026, 4, 16, 18, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void SellEdge_AvgCovMinusClient()
    {
        var pair = new ExecutionPair
        {
            Symbol = "XAUUSD",
            Side = BridgeSide.SELL,
            ClientVolume = 1.0m,
            ClientPrice = 2000.00m,
            CovFills = new List<CovFill>
            {
                new() { Volume = 1.0m, Price = 2000.30m, TimeUtc = T0 },
            },
        };

        BridgePairingEngine.ComputeMetrics(pair);
        Assert.AreEqual(0.30m, pair.PriceEdge);
    }

    [TestMethod]
    public void BuyEdge_ClientMinusAvgCov()
    {
        var pair = new ExecutionPair
        {
            Symbol = "XAUUSD",
            Side = BridgeSide.BUY,
            ClientVolume = 1.0m,
            ClientPrice = 2000.50m,
            CovFills = new List<CovFill>
            {
                new() { Volume = 1.0m, Price = 2000.20m, TimeUtc = T0 },
            },
        };

        BridgePairingEngine.ComputeMetrics(pair);
        Assert.AreEqual(0.30m, pair.PriceEdge, "BUY: +edge when broker buys cheaper than client");
    }

    [TestMethod]
    public void ZeroEdge_WhenPricesMatch()
    {
        var pair = new ExecutionPair
        {
            Symbol = "XAUUSD",
            Side = BridgeSide.SELL,
            ClientVolume = 0.20m,
            ClientPrice = 4793.81m,
            CovFills = new List<CovFill>
            {
                new() { Volume = 0.11m, Price = 4793.81m, TimeUtc = T0 },
                new() { Volume = 0.09m, Price = 4793.81m, TimeUtc = T0 },
            },
        };

        BridgePairingEngine.ComputeMetrics(pair);
        Assert.AreEqual(0m, pair.PriceEdge);
        Assert.AreEqual(0m, pair.Pips);
    }

    [TestMethod]
    public void PipConversion_Eurusd_UsesForexPipSize()
    {
        var pair = new ExecutionPair
        {
            Symbol = "EURUSD",
            Side = BridgeSide.SELL,
            ClientVolume = 1.0m,
            ClientPrice = 1.10000m,
            CovFills = new List<CovFill>
            {
                new() { Volume = 1.0m, Price = 1.10030m, TimeUtc = T0 },
            },
        };

        BridgePairingEngine.ComputeMetrics(pair);
        Assert.AreEqual(0.00030m, pair.PriceEdge);
        Assert.AreEqual(3.0m, pair.Pips); // 0.00030 / 0.0001
    }

    [TestMethod]
    public void PipConversion_Usdjpy_UsesJpyPipSize()
    {
        var pair = new ExecutionPair
        {
            Symbol = "USDJPY",
            Side = BridgeSide.SELL,
            ClientVolume = 1.0m,
            ClientPrice = 150.000m,
            CovFills = new List<CovFill>
            {
                new() { Volume = 1.0m, Price = 150.050m, TimeUtc = T0 },
            },
        };

        BridgePairingEngine.ComputeMetrics(pair);
        Assert.AreEqual(0.050m, pair.PriceEdge);
        Assert.AreEqual(5.0m, pair.Pips); // 0.050 / 0.01
    }

    [TestMethod]
    public void PipConversion_Xauusd_UsesMetalPipSize()
    {
        var pair = new ExecutionPair
        {
            Symbol = "XAUUSD",
            Side = BridgeSide.SELL,
            ClientVolume = 0.5m,
            ClientPrice = 4793.81m,
            CovFills = new List<CovFill>
            {
                new() { Volume = 0.5m, Price = 4794.11m, TimeUtc = T0 },
            },
        };

        BridgePairingEngine.ComputeMetrics(pair);
        Assert.AreEqual(0.30m, pair.PriceEdge);
        Assert.AreEqual(3.0m, pair.Pips); // 0.30 / 0.1
    }

    [TestMethod]
    public void AvgCovPrice_IsVolumeWeighted()
    {
        var pair = new ExecutionPair
        {
            Symbol = "XAUUSD",
            Side = BridgeSide.SELL,
            ClientVolume = 1.0m,
            ClientPrice = 2000m,
            CovFills = new List<CovFill>
            {
                new() { Volume = 0.25m, Price = 2000m, TimeUtc = T0 },
                new() { Volume = 0.75m, Price = 2100m, TimeUtc = T0 },
            },
        };

        BridgePairingEngine.ComputeMetrics(pair);
        // (0.25*2000 + 0.75*2100) / 1.0 = 500 + 1575 = 2075
        Assert.AreEqual(2075m, pair.AvgCovPrice);
        Assert.AreEqual(75m, pair.PriceEdge);
    }

    [TestMethod]
    public void NoCovFills_EdgeIsZero()
    {
        var pair = new ExecutionPair
        {
            Symbol = "XAUUSD",
            Side = BridgeSide.SELL,
            ClientVolume = 1.0m,
            ClientPrice = 2000m,
            CovFills = new List<CovFill>(),
        };

        BridgePairingEngine.ComputeMetrics(pair);
        Assert.AreEqual(0m, pair.PriceEdge);
        Assert.AreEqual(0m, pair.Pips);
        Assert.AreEqual(0m, pair.CoverageRatio);
    }

    [TestMethod]
    public void PipResolver_OverrideTakesPrecedence()
    {
        try
        {
            BridgePipResolver.Overrides["XRPUSD"] = 0.00001m;
            var size = BridgePipResolver.GetPipSize("XRPUSD", 0.52m);
            Assert.AreEqual(0.00001m, size);
        }
        finally
        {
            BridgePipResolver.Overrides.TryRemove("XRPUSD", out _);
        }
    }

    [TestMethod]
    public void PipResolver_DefaultsForUnknownSymbolUsePriceHeuristic()
    {
        // Unknown 5-letter symbol with FX-range price → 0.0001 bucket
        Assert.AreEqual(0.0001m, BridgePipResolver.GetPipSize("WEIRD", 1.23m));
        // Index-range price → 0.1
        Assert.AreEqual(0.1m, BridgePipResolver.GetPipSize("US500X", 5000m));
        // Oil-range price → 0.01
        Assert.AreEqual(0.01m, BridgePipResolver.GetPipSize("OILX", 50m));
    }
}
