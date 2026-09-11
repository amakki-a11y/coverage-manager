using System.Text.Json;
using CoverageManager.Connector.LiveBridge;

namespace CoverageManager.Tests.LiveBridge;

/// <summary>The durable sequences: advance only forward, atomic writes, a round trip, and the cases that mean "start with a snapshot".</summary>
[TestClass]
public class FeedSequenceStoreTests
{
    private static string TempPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cm-livebridge-tests");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, Guid.NewGuid().ToString("N") + ".json");
    }

    [TestMethod]
    public void AdvancesOnlyForward_WritesThrough_AndRoundTrips()
    {
        var path = TempPath();
        using (var store = new FeedSequenceStore(path, "ws://feed/x", TimeSpan.Zero))
        {
            store.Load();
            StringAssert.Contains(store.LoadNote, "first start");
            Assert.IsTrue(store.Advance("deals", 10));
            Assert.IsFalse(store.Advance("deals", 10));
            Assert.IsFalse(store.Advance("deals", 9));
            Assert.IsTrue(store.Advance("ticks", 3));
            Assert.IsFalse(store.Advance("orders", 1));
            Assert.AreEqual(10, store.Get("deals"));
            Assert.AreEqual(0, store.Get("positions"));
            Assert.IsNotNull(store.LastSavedUtc, "write-through");

            var resume = store.Resume();
            Assert.AreEqual(10, resume["deals"]);
            Assert.IsNull(resume["positions"]);
            Assert.AreEqual(3, resume["ticks"]);
        }

        var json = JsonDocument.Parse(File.ReadAllText(path)).RootElement;
        Assert.AreEqual("ws://feed/x", json.GetProperty("url").GetString());
        Assert.AreEqual(10, json.GetProperty("seq").GetProperty("deals").GetInt64());
        Assert.AreEqual(0, json.GetProperty("seq").GetProperty("accounts").GetInt64());
        Assert.IsFalse(File.Exists(path + ".tmp"));

        using var again = new FeedSequenceStore(path, "ws://feed/x", TimeSpan.Zero);
        again.Load();
        StringAssert.Contains(again.LoadNote, "resuming");
        Assert.AreEqual(10, again.Get("deals"));
        Assert.AreEqual(3, again.Get("ticks"));
    }

    [TestMethod]
    public void AnotherFeedsFile_OrAnUnreadableOne_MeansASnapshot()
    {
        var path = TempPath();
        using (var store = new FeedSequenceStore(path, "ws://feed/x", TimeSpan.Zero))
        {
            store.Advance("deals", 10);
        }

        using (var other = new FeedSequenceStore(path, "ws://feed/y", TimeSpan.Zero))
        {
            other.Load();
            StringAssert.Contains(other.LoadNote, "another feed");
            Assert.AreEqual(0, other.Get("deals"));
        }

        File.WriteAllText(path, "{ not json");
        using var broken = new FeedSequenceStore(path, "ws://feed/x", TimeSpan.Zero);
        broken.Load();
        StringAssert.Contains(broken.LoadNote, "unreadable");
        Assert.AreEqual(0, broken.Get("deals"));
    }

    [TestMethod]
    public async Task DebouncedWrites_LandAfterTheDelay_AndOnDispose()
    {
        var path = TempPath();
        var store = new FeedSequenceStore(path, "ws://feed/x", TimeSpan.FromMilliseconds(150));
        store.Advance("positions", 5);
        Assert.IsFalse(File.Exists(path), "not yet");
        await Task.Delay(600);
        Assert.IsTrue(File.Exists(path), "after the delay");
        Assert.AreEqual(5, JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("seq").GetProperty("positions").GetInt64());

        store.Advance("positions", 6);
        store.Dispose();
        Assert.AreEqual(6, JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("seq").GetProperty("positions").GetInt64(), "flushed on dispose");
        Assert.IsFalse(store.Advance("positions", 7), "disposed: no more writes");
    }
}
