using CoverageManager.Connector;

namespace CoverageManager.Tests;

/// <summary>
/// Locks the contract of the LiveBridgeApi placeholder: selecting it must never crash the
/// host - Connect fails with a descriptive error (without leaking the password), queries
/// return empty results, and the call counters still work for /api/exposure/diagnostics.
/// </summary>
[TestClass]
public class LiveBridgeApiTests
{
    [TestMethod]
    public void Connect_FailsSafely_WithDescriptiveError()
    {
        using var api = new LiveBridgeApi(new LiveBridgeOptions { Url = "wss://bridge.example/feed" });

        Assert.IsTrue(api.Initialize());
        Assert.IsFalse(api.Connect("mt5.example:443", 1065, "s3cret-pw"));
        Assert.IsFalse(api.IsConnected);
        StringAssert.Contains(api.LastError, "LiveBridgeApi");
        StringAssert.Contains(api.LastError, "wss://bridge.example/feed");
        StringAssert.Contains(api.LastError, "1065");
        Assert.IsFalse(api.LastError.Contains("s3cret-pw"), "password must never be echoed");
    }

    [TestMethod]
    public void Connect_WithoutUrl_SaysUrlNotSet()
    {
        using var api = new LiveBridgeApi();
        api.Initialize();
        Assert.IsFalse(api.Connect("srv", 1, "x"));
        StringAssert.Contains(api.LastError, "LiveBridge:Url not set");
    }

    [TestMethod]
    public void Queries_ReturnEmpty_AndCountCalls()
    {
        using var api = new LiveBridgeApi();

        Assert.AreEqual(0, api.GetPositions(1).Count);
        Assert.AreEqual(0, api.GetPositions(2).Count);
        Assert.AreEqual(0, api.GetUserLogins("*").Length);
        Assert.IsNull(api.GetUserAccount(1));
        Assert.IsNull(api.GetTickLast("EURUSD"));
        Assert.AreEqual(0, api.RequestDeals(1, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow).Count);

        Assert.AreEqual(2, api.GetPositionsCalls);
        Assert.AreEqual(1, api.GetUserLoginsCalls);
        Assert.AreEqual(1, api.GetUserAccountCalls);
        Assert.AreEqual(1, api.TickLastCalls);
        Assert.AreEqual(1, api.RequestDealsCalls);
    }

    [TestMethod]
    public void Subscriptions_ReturnFalse_AndTeardownIsNoOp()
    {
        using var api = new LiveBridgeApi();

        Assert.IsFalse(api.SelectedAddAll());
        Assert.IsFalse(api.SubscribeTicks());
        Assert.IsFalse(api.SubscribeDeals());
        Assert.IsFalse(api.SubscribePositions());
        Assert.IsFalse(api.SubscribeUsers());
        StringAssert.Contains(api.LastError, "SubscribeUsers");

        // Mirrors the teardown sequence MT5ManagerConnection runs on every reconnect.
        api.UnsubscribeTicks();
        api.UnsubscribeDeals();
        api.UnsubscribePositions();
        api.UnsubscribeUsers();
        api.Disconnect();
        Assert.IsFalse(api.IsConnected);
    }

    [TestMethod]
    public void Dispose_IsIdempotent_AndBlocksReuse()
    {
        var api = new LiveBridgeApi();
        api.Dispose();
        api.Dispose();
        Assert.ThrowsException<ObjectDisposedException>(() => api.Initialize());
    }
}

[TestClass]
public class MT5ApiFactoryTests
{
    [TestMethod]
    public void Normalize_BlankSelectsManager()
    {
        Assert.AreEqual(MT5ApiProviders.Manager, MT5ApiProviders.Normalize(null));
        Assert.AreEqual(MT5ApiProviders.Manager, MT5ApiProviders.Normalize(""));
        Assert.AreEqual(MT5ApiProviders.Manager, MT5ApiProviders.Normalize("   "));
    }

    [TestMethod]
    public void Normalize_IsCaseInsensitiveAndTrims()
    {
        Assert.AreEqual(MT5ApiProviders.LiveBridge, MT5ApiProviders.Normalize("livebridge"));
        Assert.AreEqual(MT5ApiProviders.LiveBridge, MT5ApiProviders.Normalize(" LIVEBRIDGE "));
        Assert.AreEqual(MT5ApiProviders.Manager, MT5ApiProviders.Normalize("manager"));
    }

    [TestMethod]
    public void Normalize_RejectsUnknownProvider()
    {
        var ex = Assert.ThrowsException<ArgumentException>(() => MT5ApiProviders.Normalize("Bridge"));
        StringAssert.Contains(ex.Message, "Manager");
        StringAssert.Contains(ex.Message, "LiveBridge");
    }

    [TestMethod]
    public void Factory_DefaultsToManager()
    {
        Assert.AreEqual(MT5ApiProviders.Manager, new MT5ApiFactory().ProviderName);
    }

    [TestMethod]
    public void Factory_LiveBridge_CreatesPlaceholder()
    {
        var factory = new MT5ApiFactory("LiveBridge", new LiveBridgeOptions { Url = "wss://x" });
        Assert.AreEqual(MT5ApiProviders.LiveBridge, factory.ProviderName);

        using var api = factory.Create();
        Assert.IsInstanceOfType(api, typeof(LiveBridgeApi));
        Assert.IsFalse(api.Connect("srv", 1, "pw"));
        StringAssert.Contains(api.LastError, "wss://x");
    }

    [TestMethod]
    public void Factory_UnknownProvider_FailsAtConstruction()
    {
        Assert.ThrowsException<ArgumentException>(() => new MT5ApiFactory("nope"));
    }
}
