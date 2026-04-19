using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;

namespace CoverageManager.Tests;

[TestClass]
public class ExposureEngineTests
{
    private PositionManager _pm = null!;
    private ExposureEngine _engine = null!;

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
            }
        });
        _engine = new ExposureEngine(_pm);
    }

    [TestMethod]
    public void CalculateExposure_EmptyPositions_ReturnsEmpty()
    {
        var result = _engine.CalculateExposure();
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void CalculateExposure_BBookOnly_CorrectNetVolume()
    {
        _pm.UpdateBBookPosition("bbook:1001:100", new Position
        {
            Source = "bbook", Symbol = "XAUUSD", Direction = "BUY",
            VolumeLots = 10, OpenPrice = 2650, Profit = 500
        });
        _pm.UpdateBBookPosition("bbook:1002:101", new Position
        {
            Source = "bbook", Symbol = "XAUUSD", Direction = "SELL",
            VolumeLots = 3, OpenPrice = 2660, Profit = -100
        });

        var result = _engine.CalculateExposure();
        Assert.AreEqual(1, result.Count);

        var xau = result[0];
        Assert.AreEqual("XAUUSD", xau.CanonicalSymbol);
        Assert.AreEqual(10m, xau.BBookBuyVolume);
        Assert.AreEqual(3m, xau.BBookSellVolume);
        Assert.AreEqual(7m, xau.BBookNetVolume);
        Assert.AreEqual(400m, xau.BBookPnL); // 500 + (-100)
    }

    [TestMethod]
    public void CalculateExposure_WithCoverage_CorrectHedgeRatio()
    {
        // B-Book: net +10 lots
        _pm.UpdateBBookPosition("bbook:1001:100", new Position
        {
            Source = "bbook", Symbol = "XAUUSD", Direction = "BUY",
            VolumeLots = 10, OpenPrice = 2650
        });

        // Coverage: net -5 lots (normalized from 500 GOLD lots)
        _pm.UpdateCoveragePositions(new[]
        {
            new CoveragePositionDto
            {
                Symbol = "GOLD", Direction = "SELL", Volume = 500,
                OpenPrice = 2650, Ticket = 200
            }
        });

        var result = _engine.CalculateExposure();
        Assert.AreEqual(1, result.Count);

        var xau = result[0];
        Assert.AreEqual(10m, xau.BBookNetVolume);
        Assert.AreEqual(-5m, xau.CoverageNetVolume); // 500 × (1/100) = 5, sell = -5
        Assert.AreEqual(5m, xau.NetVolume); // 10 + (-5)
        Assert.AreEqual(50m, xau.HedgeRatio); // |(-5)/10| × 100 = 50%
    }

    [TestMethod]
    public void CalculateExposure_FullyHedged_HedgeRatio100()
    {
        _pm.UpdateBBookPosition("bbook:1001:100", new Position
        {
            Source = "bbook", Symbol = "XAUUSD", Direction = "BUY",
            VolumeLots = 10, OpenPrice = 2650
        });

        _pm.UpdateCoveragePositions(new[]
        {
            new CoveragePositionDto
            {
                Symbol = "GOLD", Direction = "SELL", Volume = 1000,
                OpenPrice = 2650, Ticket = 200
            }
        });

        var result = _engine.CalculateExposure();
        var xau = result[0];
        Assert.AreEqual(0m, xau.NetVolume);
        Assert.AreEqual(100m, xau.HedgeRatio);
    }

    [TestMethod]
    public void CalculateExposure_NoBBookPositions_HedgeRatio100()
    {
        // Edge case: no B-Book = nothing to hedge = 100%
        _pm.UpdateCoveragePositions(new[]
        {
            new CoveragePositionDto
            {
                Symbol = "GOLD", Direction = "BUY", Volume = 100,
                OpenPrice = 2650, Ticket = 200
            }
        });

        var result = _engine.CalculateExposure();
        Assert.AreEqual(100m, result[0].HedgeRatio);
    }

    [TestMethod]
    public void CalculateExposure_WeightedAvgPrice_Correct()
    {
        _pm.UpdateBBookPosition("bbook:1001:100", new Position
        {
            Source = "bbook", Symbol = "XAUUSD", Direction = "BUY",
            VolumeLots = 2, OpenPrice = 2600
        });
        _pm.UpdateBBookPosition("bbook:1002:101", new Position
        {
            Source = "bbook", Symbol = "XAUUSD", Direction = "BUY",
            VolumeLots = 3, OpenPrice = 2700
        });

        var result = _engine.CalculateExposure();
        var xau = result[0];

        // Weighted avg = (2×2600 + 3×2700) / 5 = (5200 + 8100) / 5 = 2660
        Assert.AreEqual(2660m, xau.BBookBuyAvgPrice);
    }

    [TestMethod]
    public void CalculateExposure_SortedByAbsNetVolume()
    {
        _pm.LoadMappings(new[]
        {
            new SymbolMapping { CanonicalName = "XAUUSD", BBookSymbol = "XAUUSD", BBookContractSize = 100, CoverageSymbol = "GOLD", CoverageContractSize = 1, IsActive = true },
            new SymbolMapping { CanonicalName = "EURUSD", BBookSymbol = "EURUSD", BBookContractSize = 100000, CoverageSymbol = "EURUSD.r", CoverageContractSize = 100000, IsActive = true }
        });

        // EURUSD: net 2 lots
        _pm.UpdateBBookPosition("bbook:1001:100", new Position
        {
            Source = "bbook", Symbol = "EURUSD", Direction = "BUY", VolumeLots = 2
        });
        // XAUUSD: net 10 lots (bigger exposure)
        _pm.UpdateBBookPosition("bbook:1001:101", new Position
        {
            Source = "bbook", Symbol = "XAUUSD", Direction = "BUY", VolumeLots = 10
        });

        var result = _engine.CalculateExposure();
        Assert.AreEqual("XAUUSD", result[0].CanonicalSymbol);
        Assert.AreEqual("EURUSD", result[1].CanonicalSymbol);
    }

    // =========================================================================
    // Broker P&L identity: NetPnL = −(ClientPnL) + CoveragePnL
    // Locks in the CLAUDE.md P&L framework. The Exposure engine's derived
    // ExposureSummary.NetPnL must always hold this formula. A regression here
    // would push misleading P&L numbers to the dashboard — a real-money risk.
    // =========================================================================

    private void SeedBroker(decimal clientPnl, decimal covPnl)
    {
        _pm.UpdateBBookPosition("bbook:1001:100", new Position
        {
            Source = "bbook", Symbol = "XAUUSD", Direction = "BUY",
            VolumeLots = 10, OpenPrice = 2650, Profit = clientPnl,
        });
        _pm.UpdateCoveragePositions(new[]
        {
            new CoveragePositionDto
            {
                Symbol = "GOLD", Direction = "BUY", Volume = 1000,
                OpenPrice = 2650, Ticket = 200, Profit = covPnl,
            },
        });
    }

    [TestMethod]
    public void BrokerIdentity_ClientLossCoverageLoss_NetsToBrokerPerspective()
    {
        // Real scenario from 2026-04-18: client book −481,278; coverage −733,813.
        // Broker net = −(−481,278) + (−733,813) = −252,535. Broker ate the
        // coverage-leg loss minus the client-side gain.
        SeedBroker(clientPnl: -481_278m, covPnl: -733_813m);
        var xau = _engine.CalculateExposure()[0];
        Assert.AreEqual(-481_278m, xau.BBookPnL);
        Assert.AreEqual(-733_813m, xau.CoveragePnL);
        Assert.AreEqual(-252_535m, xau.NetPnL,
            "Broker net must be −(client) + coverage, not client + coverage");
    }

    [TestMethod]
    public void BrokerIdentity_ClientWinCoverageLoss_BrokerDoublyNegative()
    {
        // Worst case: clients won AND coverage lost. Broker pays both sides.
        // client +1000, cov -500  →  −(+1000) + (−500) = −1500.
        SeedBroker(clientPnl: 1_000m, covPnl: -500m);
        var xau = _engine.CalculateExposure()[0];
        Assert.AreEqual(-1_500m, xau.NetPnL);
    }

    [TestMethod]
    public void BrokerIdentity_ClientLossCoverageWin_BrokerDoublyPositive()
    {
        // Best case: clients lost AND coverage won. Broker books both.
        // client -1000, cov +500  →  −(−1000) + 500 = +1500.
        SeedBroker(clientPnl: -1_000m, covPnl: 500m);
        var xau = _engine.CalculateExposure()[0];
        Assert.AreEqual(1_500m, xau.NetPnL);
    }

    [TestMethod]
    public void BrokerIdentity_ZeroZero_NetIsZero()
    {
        SeedBroker(clientPnl: 0m, covPnl: 0m);
        var xau = _engine.CalculateExposure()[0];
        Assert.AreEqual(0m, xau.NetPnL);
    }
}
