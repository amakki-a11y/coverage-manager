using CoverageManager.Api.Services;
using CoverageManager.Core.Engines;

namespace CoverageManager.Tests;

/// <summary>Pure tests of the 12-month retention rule and the pruner schedule. The Postgres-backed
/// proof that the pruner never touches the retained window lives in PostgresServiceTests.</summary>
[TestClass]
public class RetentionPolicyTests
{
    private static DateTime U(int y, int mo, int d, int h = 0, int mi = 0, int s = 0) => new(y, mo, d, h, mi, s, DateTimeKind.Utc);

    [TestMethod]
    public void Cutoff_IsUtcMidnightMinusTwelveMonths_StableAcrossTheDay()
    {
        Assert.AreEqual(U(2025, 9, 13), RetentionPolicy.CutoffUtc(U(2026, 9, 13, 0, 0, 0), 12));
        Assert.AreEqual(U(2025, 9, 13), RetentionPolicy.CutoffUtc(U(2026, 9, 13, 23, 59, 59), 12),
            "the boundary does not move during a day");
    }

    [TestMethod]
    public void Cutoff_ClampsMonthEnds_LikePostgresInterval()
    {
        Assert.AreEqual(U(2027, 2, 28), RetentionPolicy.CutoffUtc(U(2028, 2, 29, 12), 12), "leap day -> Feb 28");
        Assert.AreEqual(U(2027, 2, 28), RetentionPolicy.CutoffUtc(U(2027, 3, 31), 1), "Mar 31 minus 1 month -> Feb 28");
    }

    [TestMethod]
    public void BelowTheDecidedTwelveMonths_IsFlagged()
    {
        Assert.IsTrue(RetentionPolicy.IsBelowDecided(11));
        Assert.IsTrue(RetentionPolicy.IsBelowDecided(0));
        Assert.IsFalse(RetentionPolicy.IsBelowDecided(12));
        Assert.IsFalse(RetentionPolicy.IsBelowDecided(24), "keeping MORE than decided is allowed");
    }

    [TestMethod]
    public void NextRun_IsTodayIfStillAhead_ElseTomorrow()
    {
        var at = new TimeSpan(3, 15, 0);
        Assert.AreEqual(U(2026, 9, 13, 3, 15), DealRetentionPruneService.NextRunUtc(U(2026, 9, 13, 1, 0), at));
        Assert.AreEqual(U(2026, 9, 14, 3, 15), DealRetentionPruneService.NextRunUtc(U(2026, 9, 13, 3, 15), at), "exactly at the slot -> next day");
        Assert.AreEqual(U(2026, 9, 14, 3, 15), DealRetentionPruneService.NextRunUtc(U(2026, 9, 13, 18, 0), at));
    }
}
