using CoverageManager.Core.Engines;
using CoverageManager.Core.Models.Bridge;

namespace CoverageManager.Tests;

[TestClass]
public class BridgePairingTests
{
    private static readonly DateTime T0 = new(2026, 4, 16, 18, 0, 0, DateTimeKind.Utc);

    private static BridgeDeal Client(string id, decimal vol, decimal price, int offsetMs = 0, BridgeSide side = BridgeSide.SELL) =>
        new()
        {
            DealId = id,
            CenOrdId = id + "-ord",
            Symbol = "XAUUSD",
            CanonicalSymbol = "XAUUSD",
            Source = BridgeSource.CLIENT,
            Side = side,
            Volume = vol,
            Price = price,
            TimeUtc = T0.AddMilliseconds(offsetMs),
        };

    private static BridgeDeal Cov(string id, decimal vol, decimal price, int offsetMs, BridgeSide side = BridgeSide.SELL) =>
        new()
        {
            DealId = id,
            CenOrdId = id + "-ord",
            Symbol = "XAUUSD",
            CanonicalSymbol = "XAUUSD",
            Source = BridgeSource.COV_OUT,
            Side = side,
            Volume = vol,
            Price = price,
            TimeUtc = T0.AddMilliseconds(offsetMs),
        };

    [TestMethod]
    public void ExactMatch_OneForOne()
    {
        var deals = new[]
        {
            Client("c1", 1.0m, 4793.81m),
            Cov("v1", 1.0m, 4793.81m, 50),
        };

        var result = BridgePairingEngine.Reconcile(deals);

        Assert.AreEqual(1, result.Pairs.Count);
        var pair = result.Pairs[0];
        Assert.AreEqual(1, pair.CovFills.Count);
        Assert.AreEqual(1.0m, pair.CovVolume);
        Assert.AreEqual(1.0m, pair.CoverageRatio);
        Assert.AreEqual(4793.81m, pair.AvgCovPrice);
        Assert.AreEqual(0m, pair.PriceEdge, "same price → zero edge");
        Assert.AreEqual(50, pair.CovFills[0].TimeDiffMs);
        Assert.AreEqual(0, result.UnattributedCoverage.Count);
    }

    [TestMethod]
    public void SplitCoverage_TwoCovOutSumToClientVolume()
    {
        var deals = new[]
        {
            Client("c1", 0.20m, 4793.81m),
            Cov("v1", 0.11m, 4793.81m, -55),
            Cov("v2", 0.09m, 4793.85m, -46),
        };

        var result = BridgePairingEngine.Reconcile(deals);
        var pair = result.Pairs.Single();

        Assert.AreEqual(2, pair.CovFills.Count);
        Assert.AreEqual(0.20m, pair.CovVolume);
        Assert.AreEqual(1.0m, pair.CoverageRatio);

        // VWAP: (0.11*4793.81 + 0.09*4793.85) / 0.20 = 4793.828
        var expectedVwap = (0.11m * 4793.81m + 0.09m * 4793.85m) / 0.20m;
        Assert.AreEqual(expectedVwap, pair.AvgCovPrice);
    }

    [TestMethod]
    public void PartialCoverage_CovLessThanClient()
    {
        var deals = new[]
        {
            Client("c1", 1.0m, 2000m),
            Cov("v1", 0.3m, 2000m, 100),
        };

        var result = BridgePairingEngine.Reconcile(deals);
        var pair = result.Pairs.Single();

        Assert.AreEqual(0.3m, pair.CovVolume);
        Assert.AreEqual(0.3m, pair.CoverageRatio);
        Assert.IsTrue(pair.CoverageRatio < 1m);
    }

    [TestMethod]
    public void OverCoverage_ExcessAttributedButTracked()
    {
        var deals = new[]
        {
            Client("c1", 0.5m, 2000m),
            Cov("v1", 0.8m, 2000m, 200),
        };

        var result = BridgePairingEngine.Reconcile(deals);
        var pair = result.Pairs.Single();

        // The one fill is taken whole (spec: we don't split a physical fill).
        Assert.AreEqual(1, pair.CovFills.Count);
        Assert.AreEqual(0.8m, pair.CovVolume);
        Assert.IsTrue(pair.CoverageRatio > 1m);
        Assert.AreEqual(1, result.OverCoveredClientCount);
    }

    [TestMethod]
    public void NegativeTimeDiff_PreHedgeCase_StillPaired()
    {
        // Centroid pre-hedge: cov fires 75ms BEFORE client is booked.
        var deals = new[]
        {
            Client("c1", 0.5m, 2000m),
            Cov("v1", 0.5m, 2001m, -75),
        };

        var result = BridgePairingEngine.Reconcile(deals);
        var pair = result.Pairs.Single();

        Assert.AreEqual(1, pair.CovFills.Count);
        Assert.AreEqual(-75, pair.CovFills[0].TimeDiffMs);
        Assert.AreEqual(-75, pair.MinTimeDiffMs);
        Assert.AreEqual(-75, pair.MaxTimeDiffMs);
    }

    [TestMethod]
    public void OutOfWindow_CovIgnored()
    {
        // Window is 10s; this cov is 15s late → must not pair.
        var deals = new[]
        {
            Client("c1", 0.5m, 2000m),
            Cov("v1", 0.5m, 2001m, 15_000),
        };

        var result = BridgePairingEngine.Reconcile(deals);
        var pair = result.Pairs.Single();

        Assert.AreEqual(0, pair.CovFills.Count);
        Assert.AreEqual(1, result.UnattributedCoverage.Count);
    }

    [TestMethod]
    public void SymbolMismatch_NotPaired()
    {
        var deals = new[]
        {
            Client("c1", 0.5m, 2000m),
            new BridgeDeal
            {
                DealId = "v1", CenOrdId = "v1-ord", Symbol = "GBPUSD", CanonicalSymbol = "GBPUSD",
                Source = BridgeSource.COV_OUT, Side = BridgeSide.SELL, Volume = 0.5m, Price = 1.25m,
                TimeUtc = T0.AddMilliseconds(50),
            },
        };

        var result = BridgePairingEngine.Reconcile(deals);
        Assert.AreEqual(0, result.Pairs[0].CovFills.Count);
        Assert.AreEqual(1, result.UnattributedCoverage.Count);
    }

    [TestMethod]
    public void SideMismatch_NotPaired()
    {
        var deals = new[]
        {
            Client("c1", 0.5m, 2000m, side: BridgeSide.SELL),
            Cov("v1", 0.5m, 2001m, 50, side: BridgeSide.BUY),
        };

        var result = BridgePairingEngine.Reconcile(deals);
        Assert.AreEqual(0, result.Pairs[0].CovFills.Count);
        Assert.AreEqual(1, result.UnattributedCoverage.Count);
    }

    [TestMethod]
    public void SymbolMappingApplied_XauusdAndGoldMatch()
    {
        // Client side carries canonical "XAUUSD"; LP side came in as "GOLD" but has been
        // canonicalized to "XAUUSD" before reaching the engine.
        var deals = new[]
        {
            new BridgeDeal
            {
                DealId = "c1", CenOrdId = "c1-ord",
                Symbol = "XAUUSD", CanonicalSymbol = "XAUUSD",
                Source = BridgeSource.CLIENT, Side = BridgeSide.SELL,
                Volume = 1.0m, Price = 2000m, TimeUtc = T0,
            },
            new BridgeDeal
            {
                DealId = "v1", CenOrdId = "c1-ord",
                Symbol = "GOLD", CanonicalSymbol = "XAUUSD",
                Source = BridgeSource.COV_OUT, Side = BridgeSide.SELL,
                Volume = 1.0m, Price = 2000.5m, TimeUtc = T0.AddMilliseconds(40),
            },
        };

        var result = BridgePairingEngine.Reconcile(deals);
        Assert.AreEqual(1, result.Pairs[0].CovFills.Count);
    }

    [TestMethod]
    public void GreedyByAbsoluteTimeDiff()
    {
        // Three coverage fills; greedy should take the closest-in-time one first.
        var deals = new[]
        {
            Client("c1", 0.3m, 2000m),
            Cov("v1", 0.3m, 2001m, 800),   // 800ms
            Cov("v2", 0.3m, 2002m, 50),    // 50ms — closest, should be picked first
            Cov("v3", 0.3m, 2003m, -200),  // 200ms
        };

        var result = BridgePairingEngine.Reconcile(deals);
        var pair = result.Pairs.Single();

        Assert.AreEqual(1, pair.CovFills.Count);
        Assert.AreEqual("v2", pair.CovFills[0].DealId);
        Assert.AreEqual(2, result.UnattributedCoverage.Count);
    }

    [TestMethod]
    public void UnclassifiedTreatedAsClient()
    {
        var deals = new[]
        {
            new BridgeDeal
            {
                DealId = "c1", CenOrdId = "c1-ord",
                Symbol = "XAUUSD", CanonicalSymbol = "XAUUSD",
                Source = BridgeSource.UNCLASSIFIED, Side = BridgeSide.SELL,
                Volume = 1.0m, Price = 2000m, TimeUtc = T0,
            },
            Cov("v1", 1.0m, 2001m, 60),
        };

        var result = BridgePairingEngine.Reconcile(deals);
        Assert.AreEqual(1, result.Pairs.Count, "UNCLASSIFIED should still yield a pair");
        Assert.AreEqual(1, result.Pairs[0].CovFills.Count);
    }
}
