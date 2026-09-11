using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;

namespace CoverageManager.Tests;

/// <summary>The changed-only account sync: the first cycle writes all, later cycles only what moved by at least a cent.</summary>
[TestClass]
public class AccountSyncTrackerTests
{
    private static TradingAccount Account(long login, decimal equity = 1000m, string group = "real-A") => new()
    {
        Source = "bbook", Login = login, Name = "Alice", GroupName = group, Leverage = 100, Balance = 1000m, Equity = equity, Credit = 0m,
        Margin = 0m, FreeMargin = equity, Currency = "USD", Status = "active", Comment = "", SyncedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
    };

    [TestMethod]
    public void FirstCycleWritesAll_ThenOnlyWhatChanged()
    {
        var tracker = new AccountSyncTracker();
        var first = tracker.Changed(new[] { Account(1), Account(2) });
        Assert.AreEqual(2, first.Count);
        tracker.MarkSynced(first);
        Assert.AreEqual(2, tracker.SyncedCount);

        Assert.AreEqual(0, tracker.Changed(new[] { Account(1), Account(2) }).Count, "fresh sync timestamps alone are not a change");
        var equity = tracker.Changed(new[] { Account(1, equity: 1000.01m), Account(2) });
        Assert.AreEqual(1, equity.Count);
        Assert.AreEqual(1L, equity[0].Login);
        Assert.AreEqual(0, tracker.Changed(new[] { Account(1, equity: 1000.004m), Account(2) }).Count, "below a cent is noise");
        Assert.AreEqual(1, tracker.Changed(new[] { Account(1), Account(2, group: "real-B") }).Count, "roster fields count");
        Assert.AreEqual(1, tracker.Changed(new[] { Account(1), Account(2), Account(3) }).Count, "a new login is written");
    }

    [TestMethod]
    public void MarkSyncedIsPartial_TheRestStaysChanged()
    {
        var tracker = new AccountSyncTracker();
        var all = new[] { Account(1), Account(2), Account(3) };
        var changed = tracker.Changed(all);
        tracker.MarkSynced(changed.Take(2));
        var rest = tracker.Changed(all);
        Assert.AreEqual(1, rest.Count);
        Assert.AreEqual(3L, rest[0].Login);
        tracker.Reset();
        Assert.AreEqual(3, tracker.Changed(all).Count);
    }

    [TestMethod]
    public void Fingerprint_IgnoresTheSyncTimestamps_AndRoundsMoneyToTheCent()
    {
        var a = Account(7, equity: 1234.5678m);
        var b = Account(7, equity: 1234.5712m);
        b.SyncedAt = a.SyncedAt.AddHours(1);
        b.UpdatedAt = a.UpdatedAt.AddHours(1);
        Assert.AreEqual(AccountSyncTracker.Fingerprint(a), AccountSyncTracker.Fingerprint(b));
        Assert.AreNotEqual(AccountSyncTracker.Fingerprint(a), AccountSyncTracker.Fingerprint(Account(7, equity: 1234.58m)));
    }
}
