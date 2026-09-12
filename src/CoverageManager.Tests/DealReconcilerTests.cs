using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;

namespace CoverageManager.Tests;

/// <summary>
/// The gate that keeps a partial deal provider (the Live Bridge feed) from deleting Supabase deals it cannot see.
/// The feed holds only the deals received since its resume point, so the comparison is clamped to that window and a
/// stored deal older than the earliest deal the feed holds is never a ghost. The Manager API (full history) behaves
/// as it always did.
/// </summary>
[TestClass]
public class DealReconcilerTests
{
    private static readonly DateTime From = new(2026, 8, 28, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc);
    // The earliest deal the feed holds (the feed test of 2026-09-11 started here).
    private static readonly DateTime FeedStart = new(2026, 9, 11, 2, 49, 52, DateTimeKind.Utc);

    private static ClosedDeal Provider(ulong id, DateTime time) => new()
    {
        DealId = id, Login = 1001, Symbol = "XAUUSD", Direction = "BUY", VolumeLots = 0.1m, Price = 2400m, Time = time, Action = 0, Entry = 1,
    };

    private static DealRecord Stored(long id, DateTime time) => new()
    {
        DealId = id, Source = "bbook", Login = 1001, Symbol = "XAUUSD", CanonicalSymbol = "XAUUSD", Direction = "BUY",
        Volume = 0.1m, Price = 2400m, DealTime = time, Action = 0, Entry = 1,
    };

    [TestMethod]
    public void ManagerApi_FullHistory_EveryStoredDealTheServerLacksIsAGhost()
    {
        var provider = new[] { Provider(1, From.AddDays(1)), Provider(2, To.AddHours(-1)) };
        var stored = new[] { Stored(1, From.AddDays(1)), Stored(3, From.AddDays(2)), Stored(4, To.AddHours(-2)) };

        var plan = DealReconciler.Plan(provider, stored, From, To, DealHistoryWindow.Full);

        Assert.IsFalse(plan.Clamped);
        Assert.AreEqual(From, plan.FromUtc);
        Assert.IsTrue(plan.Comparable);
        CollectionAssert.AreEquivalent(new long[] { 3, 4 }, plan.Ghosts.Select(d => d.DealId).ToList());
        CollectionAssert.AreEquivalent(new ulong[] { 2 }, plan.Missing.Select(d => d.DealId).ToList());
        CollectionAssert.AreEquivalent(new ulong[] { 1 }, plan.Common.Select(d => d.DealId).ToList());
        Assert.AreEqual(0, plan.Unverifiable.Count);
        Assert.AreEqual("", plan.Note);
    }

    [TestMethod]
    public void LiveBridge_AStoredDealOlderThanTheEarliestFeedDealIsKept_NotAGhost()
    {
        // The feed holds deals from FeedStart on. Supabase holds a deal from two days before that (the Manager era),
        // one from a millisecond before, and one inside the feed's window that the feed does not hold: a real ghost.
        var provider = new[] { Provider(10, FeedStart), Provider(11, FeedStart.AddMinutes(30)) };
        var preWindow = Stored(5, FeedStart.AddDays(-2));
        var justBefore = Stored(6, FeedStart.AddMilliseconds(-1));
        var inWindowGhost = Stored(7, FeedStart.AddMinutes(10));
        var stored = new[] { preWindow, justBefore, Stored(10, FeedStart), inWindowGhost };

        var plan = DealReconciler.Plan(provider, stored, From, To, DealHistoryWindow.Since(FeedStart));

        Assert.IsTrue(plan.Clamped);
        Assert.AreEqual(FeedStart, plan.FromUtc, "the comparison starts where the feed's record starts");
        Assert.AreEqual(From, plan.RequestedFromUtc);
        Assert.IsTrue(plan.Comparable);
        CollectionAssert.AreEquivalent(new long[] { 5, 6 }, plan.Unverifiable.Select(d => d.DealId).ToList(), "deals before the window are kept, not judged");
        CollectionAssert.AreEquivalent(new long[] { 7 }, plan.Ghosts.Select(d => d.DealId).ToList(), "only a deal inside the feed's window can be a ghost");
        Assert.IsFalse(plan.Ghosts.Any(d => d.DealId == 5 || d.DealId == 6), "a pre-window deal is never deleted");
        CollectionAssert.AreEquivalent(new ulong[] { 11 }, plan.Missing.Select(d => d.DealId).ToList());
        CollectionAssert.AreEquivalent(new ulong[] { 10 }, plan.Common.Select(d => d.DealId).ToList());
        Assert.AreEqual(2, plan.StoredDeals.Count);
        StringAssert.Contains(plan.Note, "2 stored deals before it kept unchecked");
    }

    [TestMethod]
    public void LiveBridge_WindowStartingBeforeTheRequestedStart_DoesNotClamp()
    {
        var plan = DealReconciler.Plan(
            new[] { Provider(1, From.AddHours(1)) },
            new[] { Stored(2, From.AddHours(2)) },
            From, To, DealHistoryWindow.Since(From.AddDays(-1)));

        Assert.IsFalse(plan.Clamped);
        Assert.AreEqual(From, plan.FromUtc);
        CollectionAssert.AreEquivalent(new long[] { 2 }, plan.Ghosts.Select(d => d.DealId).ToList());
        Assert.AreEqual(0, plan.Unverifiable.Count);
        Assert.AreEqual("", plan.Note);
    }

    [TestMethod]
    public void LiveBridge_HoldingNoDeals_ComparesNothingAndDeletesNothing()
    {
        var stored = new[] { Stored(1, From.AddDays(1)), Stored(2, To.AddHours(-1)) };

        var plan = DealReconciler.Plan(Array.Empty<ClosedDeal>(), stored, From, To, DealHistoryWindow.None);

        Assert.IsFalse(plan.Comparable);
        Assert.AreEqual(To, plan.FromUtc);
        Assert.AreEqual(0, plan.Ghosts.Count);
        Assert.AreEqual(0, plan.Missing.Count);
        Assert.AreEqual(2, plan.Unverifiable.Count);
        StringAssert.Contains(plan.Note, "nothing deleted");
    }

    [TestMethod]
    public void LiveBridge_WindowStartingAfterTheRequestedEnd_IsEmpty()
    {
        var stored = new[] { Stored(1, To.AddHours(-1)) };

        var plan = DealReconciler.Plan(Array.Empty<ClosedDeal>(), stored, From, To, DealHistoryWindow.Since(To.AddHours(1)));

        Assert.IsFalse(plan.Comparable);
        Assert.AreEqual(To, plan.FromUtc);
        Assert.AreEqual(0, plan.Ghosts.Count);
        Assert.AreEqual(1, plan.Unverifiable.Count);
    }

    [TestMethod]
    public void StoredTimesWithLocalKind_AreComparedInUtc()
    {
        // System.Text.Json gives timestamptz values Kind=Local on a box whose zone is not UTC: the same instant must
        // land on the same side of the window start whatever its Kind.
        var instantBefore = FeedStart.AddMinutes(-1);
        var instantAfter = FeedStart.AddMinutes(1);
        var stored = new[] { Stored(1, instantBefore.ToLocalTime()), Stored(2, instantAfter.ToLocalTime()) };

        var plan = DealReconciler.Plan(Array.Empty<ClosedDeal>(), stored, From, To, DealHistoryWindow.Since(FeedStart));

        CollectionAssert.AreEquivalent(new long[] { 1 }, plan.Unverifiable.Select(d => d.DealId).ToList());
        CollectionAssert.AreEquivalent(new long[] { 2 }, plan.Ghosts.Select(d => d.DealId).ToList());
    }

    [TestMethod]
    public void Window_CoversAndDescribesItself()
    {
        Assert.IsTrue(DealHistoryWindow.Full.Covers(DateTime.MinValue));
        Assert.IsFalse(DealHistoryWindow.None.Covers(DateTime.MaxValue));
        Assert.IsTrue(DealHistoryWindow.None.IsEmpty);
        var since = DealHistoryWindow.Since(FeedStart);
        Assert.IsFalse(since.IsEmpty);
        Assert.IsTrue(since.Covers(FeedStart));
        Assert.IsFalse(since.Covers(FeedStart.AddTicks(-1)));
        StringAssert.Contains(since.ToString(), "2026-09-11T02:49:52");
    }
}
