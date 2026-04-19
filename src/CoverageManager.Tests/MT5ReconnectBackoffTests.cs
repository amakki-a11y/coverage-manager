using CoverageManager.Connector;

namespace CoverageManager.Tests;

/// <summary>
/// Locks the exponential-backoff progression MT5ManagerConnection uses to
/// reconnect after a drop. The spec says 1s → 60s doubling; a regression here
/// would either hammer the MT5 server (if min shrank) or leave coverage
/// silently stale for minutes (if max grew).
/// </summary>
[TestClass]
public class MT5ReconnectBackoffTests
{
    [TestMethod]
    public void NextBackoffMs_DoublesFromInitial()
    {
        Assert.AreEqual(2000, MT5ManagerConnection.NextBackoffMs(1000));
        Assert.AreEqual(4000, MT5ManagerConnection.NextBackoffMs(2000));
        Assert.AreEqual(8000, MT5ManagerConnection.NextBackoffMs(4000));
    }

    [TestMethod]
    public void NextBackoffMs_CapsAtMax()
    {
        // 32s → 60s cap (next double would be 64s).
        Assert.AreEqual(60000, MT5ManagerConnection.NextBackoffMs(32000));
        // Already at max — still at max.
        Assert.AreEqual(60000, MT5ManagerConnection.NextBackoffMs(60000));
        // Over-max input stays clamped.
        Assert.AreEqual(60000, MT5ManagerConnection.NextBackoffMs(120000));
    }

    [TestMethod]
    public void NextBackoffMs_HandlesZeroAndNegativeInput()
    {
        // Zero / negative shouldn't underflow to 0 forever.
        Assert.IsTrue(MT5ManagerConnection.NextBackoffMs(0) >= 2);
        Assert.IsTrue(MT5ManagerConnection.NextBackoffMs(-5) >= 2);
    }

    [TestMethod]
    public void BackoffSequence_From1s_ReachesCapInAtMost7Steps()
    {
        // 1s → 2 → 4 → 8 → 16 → 32 → 60 (capped). Six doublings.
        // We over-budget at 7 to tolerate off-by-one future tweaks.
        var ms = MT5ManagerConnection.InitialBackoffMs;
        var steps = 0;
        while (ms < MT5ManagerConnection.MaxBackoffMs && steps < 20)
        {
            ms = MT5ManagerConnection.NextBackoffMs(ms);
            steps++;
        }
        Assert.AreEqual(MT5ManagerConnection.MaxBackoffMs, ms);
        Assert.IsTrue(steps <= 7, $"Expected ≤7 doublings to reach {MT5ManagerConnection.MaxBackoffMs}ms cap, took {steps}");
    }
}
