using CoverageManager.Connector;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoverageManager.Tests;

/// <summary>
/// The connection's deal reloads (the bring-up backfill, and ReloadDeals behind /api/exposure/pnl/reload and
/// /api/accounts/backfill-deals) under a provider that cannot answer from the server's whole history: the deals
/// already in memory that the provider cannot re-supply stay, instead of being cleared and lost until the next
/// restart. With the full history (the Manager API) a reload still replaces the store, as it always did.
/// </summary>
[TestClass]
public class MT5ManagerConnectionDealHistoryTests
{
#pragma warning disable CS0067 // the fake never raises its events
    private sealed class FakeApi : IMT5Api, IMT5DealHistory
    {
        private long _requestDealsCalls;

        public DealHistoryWindow DealHistory { get; set; } = DealHistoryWindow.Full;
        public List<RawDeal> Deals { get; } = new();
        public bool IsConnected { get; private set; }
        public string LastError => "";

        public event Action<RawTick>? OnTick;
        public event Action<RawDeal>? OnDealAdd;
        public event Action<RawPosition>? OnPositionAdd;
        public event Action<RawPosition>? OnPositionUpdate;
        public event Action<RawPosition>? OnPositionDelete;
        public event Action<RawAccount>? OnUserUpdate;

        public bool Initialize() => true;
        public bool Connect(string server, ulong login, string password, uint timeoutMs = 30000) { IsConnected = true; return true; }
        public void Disconnect() => IsConnected = false;
        public bool SubscribeTicks(string symbolMask = "*") => true;
        public void UnsubscribeTicks() { }
        public bool SelectedAddAll() => true;
        public bool SubscribeDeals() => true;
        public void UnsubscribeDeals() { }
        public bool SubscribePositions() => true;
        public void UnsubscribePositions() { }
        public bool SubscribeUsers() => true;
        public void UnsubscribeUsers() { }

        public List<RawDeal> RequestDeals(ulong login, DateTimeOffset from, DateTimeOffset to)
        {
            Interlocked.Increment(ref _requestDealsCalls);
            var fromMs = from.ToUnixTimeMilliseconds();
            var toMs = to.ToUnixTimeMilliseconds();
            return Deals.Where(d => d.Login == login && d.TimeMsc >= fromMs && d.TimeMsc <= toMs).ToList();
        }

        public List<RawPosition> GetPositions(ulong login) => new();
        public ulong[] GetUserLogins(string groupMask) => new ulong[] { 1001 };
        public RawTick? GetTickLast(string symbol) => null;
        public RawAccount? GetUserAccount(ulong login) => null;

        public long GetPositionsCalls => 0;
        public long GetUserAccountCalls => 0;
        public long GetUserLoginsCalls => 0;
        public long RequestDealsCalls => Interlocked.Read(ref _requestDealsCalls);
        public long TickLastCalls => 0;

        public void Dispose() { }
    }
#pragma warning restore CS0067

    private sealed class FakeFactory : IMT5ApiFactory
    {
        public FakeApi Api { get; } = new();
        public string ProviderName => MT5ApiProviders.LiveBridge;
        public bool RequiresManagerAccount => false;
        public string? Endpoint => "ws://127.0.0.1:1/feed/test";
        public IMT5Api Create() => Api;
    }

    private static RawDeal Deal(ulong id, DateTime timeUtc) => new()
    {
        DealId = id, Login = 1001, TimeMsc = new DateTimeOffset(timeUtc).ToUnixTimeMilliseconds(), Symbol = "XAUUSD", Action = 0,
        VolumeRaw = 1000, Price = 2400m, Profit = 0, Commission = 0, Storage = 0, Fee = 0, Entry = 1, OrderId = 1, PositionId = 1, Comment = "",
    };

    private static ClosedDeal Closed(ulong id, DateTime timeUtc) => new()
    {
        DealId = id, Login = 1001, Symbol = "XAUUSD", Direction = "BUY", VolumeLots = 0.1m, Price = 2400m, Time = timeUtc, Action = 0, Entry = 1,
    };

    private static async Task WaitUntilAsync(Func<bool> condition, string what, int timeoutMs = 15_000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (!condition())
        {
            if (Environment.TickCount64 > deadline) Assert.Fail("timed out waiting for " + what);
            await Task.Delay(20);
        }
    }

    private static MT5ManagerConnection NewConnection(FakeFactory factory, DealStore store) => new(
        NullLogger<MT5ManagerConnection>.Instance, new PositionManager(), new PriceCache(), store,
        () => Task.FromResult(new List<AccountSettings>()), () => { }, apiFactory: factory);

    [TestMethod]
    public async Task PartialProvider_BringUpAndReload_KeepTheDealsTheFeedCannotResupply()
    {
        var feedStart = DateTime.UtcNow.AddHours(-1);
        var factory = new FakeFactory();
        factory.Api.DealHistory = DealHistoryWindow.Since(feedStart);
        factory.Api.Deals.Add(Deal(10, feedStart.AddMinutes(5)));
        var store = new DealStore();
        store.AddDeal(Closed(5, feedStart.AddDays(-2)));   // loaded from Supabase at startup: before the feed's window

        var connection = NewConnection(factory, store);
        await connection.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(() => factory.Api.RequestDealsCalls > 0, "the bring-up backfill");
            Assert.IsTrue(store.GetAllDeals().Any(d => d.DealId == 5), "the pre-window deal survived the bring-up backfill");
            Assert.AreEqual(DealHistoryWindow.Since(feedStart), connection.DealHistory);

            var count = connection.ReloadDeals(new DateTimeOffset(feedStart.AddDays(-7)), new DateTimeOffset(feedStart.AddDays(1)));
            Assert.AreEqual(1, count, "the feed answered with the one deal it holds");
            Assert.IsTrue(store.GetAllDeals().Any(d => d.DealId == 5), "the pre-window deal survived ReloadDeals");
            Assert.IsTrue(store.GetAllDeals().Any(d => d.DealId == 10), "the feed's deal was loaded");
            Assert.AreEqual(2, store.DealCount);
        }
        finally
        {
            await connection.StopAsync(CancellationToken.None);
        }
    }

    [TestMethod]
    public async Task FullProvider_ReloadStillReplacesTheStore()
    {
        var feedStart = DateTime.UtcNow.AddHours(-1);
        var factory = new FakeFactory();
        factory.Api.DealHistory = DealHistoryWindow.Full;
        factory.Api.Deals.Add(Deal(10, feedStart.AddMinutes(5)));
        var store = new DealStore();
        store.AddDeal(Closed(5, feedStart.AddDays(-2)));

        var connection = NewConnection(factory, store);
        await connection.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(() => factory.Api.RequestDealsCalls > 0, "the bring-up backfill");
            Assert.IsFalse(store.GetAllDeals().Any(d => d.DealId == 5), "with the full history the bring-up backfill replaces the store");
            Assert.AreEqual(DealHistoryWindow.Full, connection.DealHistory);

            var count = connection.ReloadDeals(new DateTimeOffset(feedStart.AddDays(-7)), new DateTimeOffset(feedStart.AddDays(1)));
            Assert.AreEqual(1, count);
            Assert.AreEqual(1, store.DealCount);
            Assert.IsTrue(store.GetAllDeals().Any(d => d.DealId == 10));
        }
        finally
        {
            await connection.StopAsync(CancellationToken.None);
        }
    }
}
