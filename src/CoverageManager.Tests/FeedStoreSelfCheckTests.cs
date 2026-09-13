using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;

namespace CoverageManager.Tests;

/// <summary>Pure tests of the feed/store self-check planner (no database). The integration test that
/// runs the service against a real Postgres store lives in PostgresServiceTests.</summary>
[TestClass]
public class FeedStoreSelfCheckTests
{
    private static readonly DateTime Now = new(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DealHistoryWindow Window = DealHistoryWindow.Since(Now.AddHours(-48));

    private static DealRecord D(long id, decimal profit = 10m, int action = 1, DateTime? at = null, string canonical = "XAUUSD") => new()
    {
        Source = "bbook", DealId = id, Login = 5001, Symbol = "XAUUSD-", CanonicalSymbol = canonical,
        Direction = "SELL", Action = action, Entry = 1, Volume = 1m, Price = 2400m,
        Profit = profit, Commission = -1m, Swap = 0m, Fee = 0m, DealTime = at ?? Now.AddHours(-1),
    };

    private static SelfCheckPlan Plan(IReadOnlyList<DealRecord> feed, IReadOnlyList<DealRecord> store, DealHistoryWindow? w = null) =>
        FeedStoreSelfCheck.Plan(feed, store, w ?? Window, Now, TimeSpan.FromHours(48), TimeSpan.FromDays(1));

    [TestMethod]
    public void Identical_NothingToRewrite()
    {
        var p = Plan(new[] { D(1), D(2) }, new[] { D(1), D(2) });
        Assert.IsFalse(p.Skipped);
        Assert.AreEqual(0, p.Rewrite.Count);
        Assert.AreEqual(0, p.StoreOnly);
    }

    [TestMethod]
    public void MissingAndModified_AreRewritten_StoreOnly_IsCountedNotDeleted()
    {
        var feed  = new[] { D(1), D(2, profit: 99m), D(3) };          // 3 is missing from the store
        var store = new[] { D(1), D(2, profit: 10m), D(4) };          // 4 exists only in the store

        var p = Plan(feed, store);

        CollectionAssert.AreEqual(new long[] { 3 }, p.Missing.Select(d => d.DealId).ToArray());
        CollectionAssert.AreEqual(new long[] { 2 }, p.Modified.Select(d => d.DealId).ToArray());
        Assert.AreEqual(99m, p.Modified[0].Profit, "the feed's value wins");
        Assert.AreEqual(1, p.StoreOnly);
        Assert.IsFalse(p.Rewrite.Any(d => d.DealId == 4), "a store-only deal is never part of the re-write");
        StringAssert.Contains(p.Note, "never deleted");
        // The plan type carries no delete list at all: "never delete" is structural, not a flag.
        Assert.IsNull(typeof(SelfCheckPlan).GetProperties().FirstOrDefault(pi => pi.Name.Contains("Delet")));
    }

    [TestMethod]
    public void Modified_KeepsTheStoredCanonicalSymbol()
    {
        var p = Plan(new[] { D(2, profit: 99m, canonical: "XAUUSD") },
                     new[] { D(2, profit: 10m, canonical: "LEGACY-KEY") });
        Assert.AreEqual("LEGACY-KEY", p.Modified.Single().CanonicalSymbol,
            "values are healed; the aggregation key of a historical deal is never moved");
    }

    [TestMethod]
    public void CanonicalDifferenceAlone_IsNotADivergence()
    {
        var p = Plan(new[] { D(2, canonical: "XAUUSD") }, new[] { D(2, canonical: "LEGACY-KEY") });
        Assert.AreEqual(0, p.Rewrite.Count);
    }

    [TestMethod]
    public void OutsideTheRetainedWindow_AndCashDeals_AreIgnored()
    {
        var old  = D(10, at: Now.AddDays(-30));        // before the feed's window
        var cash = D(11, action: 2);                    // BALANCE: the feed query returns trade deals only
        var p = Plan(new[] { D(1) }, new[] { D(1), old, cash });
        Assert.AreEqual(0, p.StoreOnly, "an old deal or a cash movement is not a store-only divergence");
        Assert.AreEqual(1, p.StoredDealCount);
    }

    [TestMethod]
    public void EmptyFeedWindow_Skips()
    {
        var p = Plan(Array.Empty<DealRecord>(), new[] { D(1) }, DealHistoryWindow.None);
        Assert.IsTrue(p.Skipped);
        Assert.AreEqual(0, p.Rewrite.Count);
        StringAssert.Contains(p.Note, "skipped");
    }
}
