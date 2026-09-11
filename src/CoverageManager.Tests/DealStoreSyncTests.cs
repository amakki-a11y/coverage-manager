using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;

namespace CoverageManager.Tests;

/// <summary>The deal store's persistence queue and bounds: what the incremental deal sync relies on.</summary>
[TestClass]
public class DealStoreSyncTests
{
    private static readonly DateTime T0 = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);

    private static ClosedDeal Deal(ulong id, DateTime? time = null, decimal profit = 0m, ulong login = 1001) => new()
    {
        DealId = id, Login = login, Symbol = "XAUUSD", Direction = "BUY", VolumeLots = 0.1m, Price = 2400m, Profit = profit,
        Commission = -0.1m, Swap = 0m, Fee = 0m, Entry = 1, OrderId = id + 1000, PositionId = id + 2000, Time = time ?? T0, Action = 0,
    };

    [TestMethod]
    public void NewAndChangedDealsArePending_TheSameDealTwiceIsNot()
    {
        var store = new DealStore();
        Assert.AreEqual(DealStoreChange.Added, store.AddDeal(Deal(1)));
        Assert.AreEqual(1, store.PendingCount);
        Assert.AreEqual(DealStoreChange.Unchanged, store.AddDeal(Deal(1)));
        Assert.AreEqual(1, store.PendingCount);
        Assert.AreEqual(DealStoreChange.Updated, store.AddDeal(Deal(1, profit: 5m)));
        Assert.AreEqual(1, store.PendingCount, "still one pending deal, at a newer version");
        var mods = store.TakeModifications();
        Assert.AreEqual(1, mods.Count);
        Assert.AreEqual(0m, mods[0].Before.Profit);
        Assert.AreEqual(5m, mods[0].After.Profit);
        Assert.AreEqual(0, store.PendingModificationCount);
    }

    [TestMethod]
    public void LoadedDealsAreHeldButNotPending_AndNeverOverwriteALiveDeal()
    {
        var store = new DealStore();
        store.AddDeal(Deal(1, profit: 5m));
        var added = store.Load(new[] { Deal(1, profit: 0m), Deal(2) });
        Assert.AreEqual(1, added);
        Assert.AreEqual(2, store.DealCount);
        Assert.AreEqual(1, store.PendingCount, "the loaded deal is not queued");
        Assert.AreEqual(5m, store.GetAllDeals().Single(d => d.DealId == 1).Profit, "the live copy wins over the loaded one");
        Assert.AreEqual(0, store.TakeModifications().Count);
    }

    [TestMethod]
    public void TakePending_IsOldestFirstAndBounded_MarkSyncedHonoursTheVersion()
    {
        var store = new DealStore();
        store.AddDeal(Deal(3, T0.AddMinutes(2)));
        store.AddDeal(Deal(1, T0));
        store.AddDeal(Deal(2, T0.AddMinutes(1)));
        var taken = store.TakePending(2);
        CollectionAssert.AreEqual(new ulong[] { 1, 2 }, taken.Select(p => p.Deal.DealId).ToList());

        store.AddDeal(Deal(1, T0, profit: 9m));   // changes while the batch is in flight
        Assert.AreEqual(1, store.MarkSynced(taken), "only the unchanged deal is cleared");
        Assert.AreEqual(2, store.PendingCount);
        var again = store.TakePending(10);
        CollectionAssert.AreEqual(new ulong[] { 1, 3 }, again.Select(p => p.Deal.DealId).ToList());
        Assert.AreEqual(9m, again.Single(p => p.Deal.DealId == 1).Deal.Profit);
    }

    [TestMethod]
    public void Prune_ForgetsPersistedOldDeals_KeepsUnsyncedOnes_RecomputesEarliest()
    {
        var store = new DealStore();
        store.AddDeal(Deal(1, T0.AddDays(-3)));
        store.MarkSynced(store.TakePending(10));
        store.AddDeal(Deal(2, T0.AddDays(-3)));   // old but never written
        store.AddDeal(Deal(3, T0));

        var first = store.Prune(T0.AddDays(-2), hardCap: 0);
        Assert.AreEqual(1, first.EvictedSynced);
        Assert.AreEqual(0, first.EvictedUnsynced);
        Assert.AreEqual(2, first.Remaining);
        Assert.AreEqual(T0.AddDays(-3), store.EarliestDealTime, "the unsynced old deal still anchors the window");

        store.MarkSynced(store.TakePending(10));
        var second = store.Prune(T0.AddDays(-2), hardCap: 0);
        Assert.AreEqual(1, second.EvictedSynced);
        Assert.AreEqual(T0, store.EarliestDealTime);
        Assert.AreEqual(0, store.PendingCount);
    }

    [TestMethod]
    public void Prune_HardCapDropsTheOldestEvenIfUnsynced()
    {
        var store = new DealStore();
        for (var i = 1UL; i <= 5; i++) store.AddDeal(Deal(i, T0.AddMinutes(i)));
        var result = store.Prune(T0.AddYears(-1), hardCap: 3);
        Assert.AreEqual(2, result.EvictedUnsynced);
        Assert.AreEqual(0, result.EvictedSynced);
        Assert.AreEqual(3, result.Remaining);
        CollectionAssert.AreEquivalent(new ulong[] { 3, 4, 5 }, store.GetAllDeals().Select(d => d.DealId).ToList());
        Assert.AreEqual(3, store.PendingCount);
        Assert.AreEqual(T0.AddMinutes(3), store.EarliestDealTime);
    }

    [TestMethod]
    public void RemoveAndClear_DropThePendingEntriesToo()
    {
        var store = new DealStore();
        store.AddDeal(Deal(1));
        store.AddDeal(Deal(2));
        Assert.AreEqual(1, store.RemoveDeals(new ulong[] { 1 }));
        Assert.AreEqual(1, store.PendingCount);
        Assert.AreEqual(1, store.DealCount);
        store.Clear();
        Assert.AreEqual(0, store.PendingCount);
        Assert.AreEqual(0, store.DealCount);
        Assert.IsNull(store.EarliestDealTime);
    }

    [TestMethod]
    public void SameValues_ComparesInstantsNotKinds()
    {
        var utc = Deal(1, T0);
        var local = Deal(1, T0.ToLocalTime());
        Assert.IsTrue(DealStore.SameValues(utc, local));
        var store = new DealStore();
        store.AddDeal(utc);
        Assert.AreEqual(DealStoreChange.Unchanged, store.AddDeal(local), "the same deal read back with Kind=Local is not a change");
        Assert.IsFalse(DealStore.SameValues(utc, Deal(1, T0, profit: 1m)));
    }

    [TestMethod]
    public void TodayPnL_IsCachedUntilTheStoreChanges_AndCoversTodayOnly()
    {
        var store = new DealStore();
        var today = DateTime.UtcNow.Date.AddHours(1);
        store.AddDeal(Deal(1, today, profit: 5m));
        store.AddDeal(Deal(2, today.AddDays(-1), profit: 100m));   // yesterday: not in today's figure
        var v1 = store.ChangeVersion;

        var first = store.GetTodayPnLBySymbol(TimeSpan.FromMinutes(5));
        Assert.AreEqual(5m, first.Single(p => p.Symbol == "XAUUSD").TotalProfit);
        Assert.AreSame(first, store.GetTodayPnLBySymbol(TimeSpan.FromMinutes(5)), "unchanged store: the cached list is returned");

        store.AddDeal(Deal(3, today, profit: 3m));
        Assert.AreNotEqual(v1, store.ChangeVersion);
        Assert.AreSame(first, store.GetTodayPnLBySymbol(TimeSpan.FromMinutes(5)), "inside the age window a change waits for the next recompute (the throttle)");
        var second = store.GetTodayPnLBySymbol(TimeSpan.Zero);
        Assert.AreNotSame(first, second, "past the age window the change is picked up");
        Assert.AreEqual(8m, second.Single(p => p.Symbol == "XAUUSD").TotalProfit);
        Assert.AreSame(second, store.GetTodayPnLBySymbol(TimeSpan.Zero), "no change since: no recompute even with a zero age");

        store.RemoveDeals(new ulong[] { 3 });
        Assert.AreEqual(5m, store.GetTodayPnLBySymbol(TimeSpan.Zero).Single(p => p.Symbol == "XAUUSD").TotalProfit);
    }

    [TestMethod]
    public void Audit_ListsOnlyTheChangedFields()
    {
        var entries = DealChangeAudit.Entries(new DealModification(Deal(1, profit: 0m), Deal(1, profit: 5m), T0), "bbook");
        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("profit", entries[0].FieldChanged);
        Assert.AreEqual("0.00", entries[0].OldValue);
        Assert.AreEqual("5.00", entries[0].NewValue);
        Assert.AreEqual(1L, entries[0].DealId);
        Assert.AreEqual(1001L, entries[0].Login);
        Assert.AreEqual(T0, entries[0].DetectedAt);
    }
}
