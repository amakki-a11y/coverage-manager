using CoverageManager.Connector;
using CoverageManager.Connector.LiveBridge;

namespace CoverageManager.Tests.LiveBridge;

/// <summary>The consumer's book: idempotent by identity, full-state updates, snapshot reconciliation, the queries, and the group mask.</summary>
[TestClass]
public class FeedBookTests
{
    private static RawPosition Position(ulong id, ulong login = 1001, decimal priceCurrent = 2401m) => new()
    {
        PositionId = id, Login = login, Symbol = "XAUUSD", Action = 0, Volume = 0.1m, PriceOpen = 2400m, PriceCurrent = priceCurrent, Profit = 1m, Storage = 0m, TimeMsc = 1_789_000_000_000,
    };

    private static RawDeal Deal(ulong id, ulong login = 1001, long timeMsc = 1_789_000_000_000, decimal profit = 0m) => new()
    {
        DealId = id, Login = login, TimeMsc = timeMsc, Symbol = "XAUUSD", Action = 0, VolumeRaw = 1000, Price = 2400m, Profit = profit, Commission = 0m, Storage = 0m, Fee = 0m,
        Entry = 0, OrderId = 1, PositionId = 1, Comment = "",
    };

    private static RawAccount Account(ulong login, string group, decimal balance = 100m) => new()
    {
        Login = login, Name = "", Group = group, Leverage = 100, Balance = balance, Equity = balance, Credit = 0m, Margin = 0m, FreeMargin = balance, Currency = "USD",
        RegistrationTime = 0, LastTradeTime = 0, Comment = "", BalancePrevDay = 0m, EquityPrevDay = 0m,
    };

    [TestMethod]
    public void Positions_AreIdempotentByIdentity_UpdatesReplaceTheWholeRecord_DeleteCloses()
    {
        var book = new FeedBook();
        Assert.AreEqual(FeedApply.Added, book.ApplyPosition("add", Position(1)));
        Assert.AreEqual(FeedApply.Unchanged, book.ApplyPosition("add", Position(1)));
        Assert.AreEqual(FeedApply.Unchanged, book.ApplyPosition("update", Position(1)));
        Assert.AreEqual(FeedApply.Updated, book.ApplyPosition("update", Position(1, priceCurrent: 2410m)));
        Assert.AreEqual(2410m, book.Positions(1001).Single().PriceCurrent);
        Assert.AreEqual(FeedApply.Deleted, book.ApplyPosition("delete", Position(1)));
        Assert.AreEqual(FeedApply.Unchanged, book.ApplyPosition("delete", Position(1)));
        Assert.AreEqual(0, book.PositionCount);
    }

    [TestMethod]
    public void ASnapshotClosesWhatItDoesNotMention()
    {
        var book = new FeedBook();
        book.ApplyPosition("add", Position(1));
        book.ApplyPosition("add", Position(2));
        book.ApplyPosition("add", Position(3, login: 2002));
        Assert.AreEqual(0, book.EndPositionSnapshot().Count, "no snapshot in progress: nothing closed");

        book.BeginPositionSnapshot();
        Assert.IsTrue(book.PositionSnapshotActive);
        book.ApplyPosition("add", Position(2));
        book.ApplyPosition("add", Position(4));
        var gone = book.EndPositionSnapshot();
        CollectionAssert.AreEquivalent(new ulong[] { 1, 3 }, gone.Select(p => p.PositionId).ToArray());
        CollectionAssert.AreEqual(new ulong[] { 2, 4 }, book.Positions(1001).Select(p => p.PositionId).ToArray());
        Assert.IsFalse(book.PositionSnapshotActive);
    }

    [TestMethod]
    public void Deals_AreKeptByNumber_ServedByLoginAndWindow_AndPruned()
    {
        var book = new FeedBook();
        Assert.AreEqual(FeedApply.Added, book.ApplyDeal(Deal(10, timeMsc: 1_000)));
        Assert.AreEqual(FeedApply.Unchanged, book.ApplyDeal(Deal(10, timeMsc: 1_000)));
        Assert.AreEqual(FeedApply.Updated, book.ApplyDeal(Deal(10, timeMsc: 1_000, profit: 5m)), "a deal update (the Manager API forwards those as adds too)");
        book.ApplyDeal(Deal(11, timeMsc: 2_000));
        book.ApplyDeal(Deal(12, timeMsc: 3_000));
        book.ApplyDeal(Deal(13, login: 2002, timeMsc: 2_500));

        CollectionAssert.AreEqual(new ulong[] { 10, 11, 12 }, book.Deals(1001, 0, 10_000).Select(d => d.DealId).ToArray());
        CollectionAssert.AreEqual(new ulong[] { 11 }, book.Deals(1001, 2_000, 2_999).Select(d => d.DealId).ToArray(), "the window is inclusive");
        CollectionAssert.AreEqual(new ulong[] { 13 }, book.Deals(2002, 0, 10_000).Select(d => d.DealId).ToArray());
        Assert.AreEqual(2, book.PruneDeals(2_500));
        Assert.AreEqual(2, book.DealCount);
        CollectionAssert.AreEqual(new ulong[] { 12 }, book.Deals(1001, 0, 10_000).Select(d => d.DealId).ToArray());
    }

    [TestMethod]
    public void Accounts_AreFullState_AndLoginsFollowTheGroupMask()
    {
        var book = new FeedBook();
        Assert.AreEqual(FeedApply.Added, book.ApplyAccount(Account(1001, "real\\A-Book")));
        Assert.AreEqual(FeedApply.Unchanged, book.ApplyAccount(Account(1001, "real\\A-Book")));
        Assert.AreEqual(FeedApply.Updated, book.ApplyAccount(Account(1001, "real\\A-Book", balance: 200m)));
        book.ApplyAccount(Account(2002, "demo\\B"));
        book.ApplyAccount(Account(3003, "real\\B-Book"));
        Assert.AreEqual(200m, book.Account(1001)!.Balance);
        Assert.IsNull(book.Account(4004));

        CollectionAssert.AreEqual(new ulong[] { 1001, 2002, 3003 }, book.Logins("*"));
        CollectionAssert.AreEqual(new ulong[] { 1001, 2002, 3003 }, book.Logins(""));
        CollectionAssert.AreEqual(new ulong[] { 1001, 3003 }, book.Logins("real\\*"));
        CollectionAssert.AreEqual(new ulong[] { 1001 }, book.Logins("*A-Book"));
        CollectionAssert.AreEqual(new ulong[] { 2002 }, book.Logins("!real\\*,*"));
        CollectionAssert.AreEqual(new ulong[] { 2002, 3003 }, book.Logins("demo*;real\\B*"));
        CollectionAssert.AreEqual(new ulong[] { 3003 }, book.Logins("REAL\\b-book"), "case-insensitive like MT5");
        Assert.AreEqual(0, book.Logins("nothing\\*").Length);
    }

    [TestMethod]
    public void Ticks_KeepTheLastPerSymbol()
    {
        var book = new FeedBook();
        book.ApplyTick(new RawTick { Symbol = "XAUUSD", Bid = 1m, Ask = 2m, TimeMsc = 1 });
        book.ApplyTick(new RawTick { Symbol = "XAUUSD", Bid = 3m, Ask = 4m, TimeMsc = 2 });
        book.ApplyTick(new RawTick { Symbol = "EURUSD", Bid = 1.1m, Ask = 1.2m, TimeMsc = 3 });
        Assert.AreEqual(3m, book.Tick("XAUUSD")!.Bid);
        Assert.AreEqual(2, book.SymbolCount);
        Assert.IsNull(book.Tick("GBPUSD"));
    }

    [TestMethod]
    public void GroupMask_Semantics()
    {
        Assert.IsTrue(GroupMask.Matches(null, "anything"));
        Assert.IsTrue(GroupMask.Matches("*", "anything"));
        Assert.IsTrue(GroupMask.Matches("real\\*", "real\\Gold"));
        Assert.IsFalse(GroupMask.Matches("real\\*", "demo\\Gold"));
        Assert.IsTrue(GroupMask.Matches("real\\?old", "real\\Gold"));
        Assert.IsFalse(GroupMask.Matches("!demo*", "demo\\x"));
        Assert.IsTrue(GroupMask.Matches("!demo*", "real\\x"), "only excludes: everything else passes");
        Assert.IsTrue(GroupMask.Matches("a*, b*", "Bravo"));
        Assert.IsFalse(GroupMask.Matches("a*, b*", "Charlie"));
        Assert.IsTrue(GroupMask.Matches("*", null));
        Assert.IsFalse(GroupMask.Matches("real\\*", null));
    }
}
