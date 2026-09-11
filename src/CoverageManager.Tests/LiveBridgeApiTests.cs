using System.Collections.Concurrent;
using System.Text.Json;
using CoverageManager.Connector;
using CoverageManager.Tests.LiveBridge;

namespace CoverageManager.Tests;

/// <summary>
/// LiveBridgeApi against a loopback feed that speaks the consumer contract (TheBridge spec section 15, proposal 14):
/// the subscribe with the four resume sequences, snapshot then live records, the sequence rule, durable resume after a
/// restart, reconnect after a bye and after silence, the tick replay gap, snapshot reconciliation, acks, and the
/// refusals. Nothing here touches a real bridge or an MT5 server.
/// </summary>
[TestClass]
public class LiveBridgeApiTests
{
    private const string Key = FakeFeedServer.DefaultKey;

    private static string TempStatePath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cm-livebridge-tests");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, Guid.NewGuid().ToString("N") + ".json");
    }

    private static LiveBridgeOptions TestOptions(FakeFeedServer server, string statePath, string key = Key) => new()
    {
        Url = server.Url,
        ApiKey = key,
        StatePath = statePath,
        ReconnectMs = 100,
        ReconnectMaxMs = 200,
        SilenceLimitMs = 10_000,
        HandshakeTimeoutMs = 5_000,
        AckEveryMs = 0,
        SequenceFlushMs = 0,
    };

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000, string? what = null)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (!condition())
        {
            if (Environment.TickCount64 > deadline) Assert.Fail("timed out waiting for " + (what ?? "the condition"));
            await Task.Delay(10);
        }
    }

    private static string P1 => FeedPayloadJson.Position(501, 1001, "XAUUSD", 0, 0.10m, 2400.5, 2401.0, 5.0, -0.2, 1_789_000_000);
    private static string P2 => FeedPayloadJson.Position(502, 1001, "EURUSD", 1, 1.00m, 1.08547, 1.08500, 47.0, 0, 1_789_000_100);
    private static string A1 => FeedPayloadJson.Account(1001, "real\\A-Book", 100, "USD", 10_000, 500, 10_552.0, 52.0, 120.0, 10_432.0, true, "Alice");
    private static string A2 => FeedPayloadJson.Account(2002, "demo\\B", 500, "EUR", 1_000, 0, 1_000, 0, 0, 1_000, true);
    private static string T1 => FeedPayloadJson.Tick("XAUUSD", 2400.9, 2401.3, 1_789_000_000_123);

    private static string Rec(string stream, string action, long seq, string payload) => FakeFeedConnection.Record(stream, action, seq, payload);

    /// <summary>Initialize + Connect on a worker (Connect blocks), the subscribe read, the snapshot handshake sent, Connect awaited.</summary>
    private static async Task<(FakeFeedConnection Connection, JsonElement Subscribe)> ConnectSnapshotAsync(
        LiveBridgeApi api, FakeFeedServer server, string[]? accounts = null, string[]? positions = null, string[]? ticks = null,
        long seqPositions = 100, long seqDeals = 200, long seqAccounts = 300, long seqTicks = 400)
    {
        Assert.IsTrue(api.Initialize(), api.LastError);
        var connect = Task.Run(() => api.Connect("mt5.example:443", 1065, "unused-password", 10_000));
        var connection = await server.NextConnectionAsync();
        var subscribe = await connection.Subscribe;
        await connection.SnapshotHandshakeAsync(accounts ?? Array.Empty<string>(), positions ?? Array.Empty<string>(), ticks ?? Array.Empty<string>(),
            seqPositions, seqDeals, seqAccounts, seqTicks);
        Assert.IsTrue(await connect, api.LastError);
        return (connection, subscribe);
    }

    private sealed class Captured
    {
        public ConcurrentQueue<RawPosition> Added { get; } = new();
        public ConcurrentQueue<RawPosition> Updated { get; } = new();
        public ConcurrentQueue<RawPosition> Deleted { get; } = new();
        public ConcurrentQueue<RawDeal> Deals { get; } = new();
        public ConcurrentQueue<RawAccount> Users { get; } = new();
        public ConcurrentQueue<RawTick> Ticks { get; } = new();

        public static Captured Attach(LiveBridgeApi api)
        {
            var c = new Captured();
            api.OnPositionAdd += p => c.Added.Enqueue(p);
            api.OnPositionUpdate += p => c.Updated.Enqueue(p);
            api.OnPositionDelete += p => c.Deleted.Enqueue(p);
            api.OnDealAdd += d => c.Deals.Enqueue(d);
            api.OnUserUpdate += a => c.Users.Enqueue(a);
            api.OnTick += t => c.Ticks.Enqueue(t);
            Assert.IsTrue(api.SelectedAddAll());
            Assert.IsTrue(api.SubscribeTicks());
            Assert.IsTrue(api.SubscribeDeals());
            Assert.IsTrue(api.SubscribePositions());
            Assert.IsTrue(api.SubscribeUsers());
            return c;
        }
    }

    [TestMethod]
    public async Task FirstStart_SubscribesWithNullResume_AppliesTheSnapshot_AndAnswersTheQueries()
    {
        await using var server = new FakeFeedServer();
        var state = TempStatePath();
        using var api = new LiveBridgeApi(TestOptions(server, state));

        var (connection, subscribe) = await ConnectSnapshotAsync(api, server,
            accounts: new[] { Rec("accounts", "state", 0, A1), Rec("accounts", "state", 0, A2) },
            positions: new[] { Rec("positions", "add", 90, P1), Rec("positions", "add", 95, P2) },
            ticks: new[] { Rec("ticks", "tick", 0, T1) });

        // The subscribe: every stream in the contract's order, null resume on a first start, the bearer key on the upgrade.
        Assert.AreEqual("subscribe", subscribe.GetProperty("type").GetString());
        CollectionAssert.AreEqual(new[] { "positions", "deals", "accounts", "ticks" },
            subscribe.GetProperty("streams").EnumerateArray().Select(e => e.GetString()).ToArray());
        foreach (var s in new[] { "positions", "deals", "accounts", "ticks" })
            Assert.AreEqual(JsonValueKind.Null, subscribe.GetProperty("resume").GetProperty(s).ValueKind, s);
        Assert.AreEqual("Bearer " + Key, connection.RequestHeaders["authorization"]);

        Assert.IsTrue(api.IsConnected);
        Assert.AreEqual("snapshot", api.Mode);
        Assert.AreEqual(FakeFeedServer.Source, api.Source);
        Assert.AreEqual("live", api.State);

        // The bring-up order's queries, answered from the book.
        CollectionAssert.AreEqual(new ulong[] { 1001, 2002 }, api.GetUserLogins("*"));
        CollectionAssert.AreEqual(new ulong[] { 1001 }, api.GetUserLogins("real\\*"));
        CollectionAssert.AreEqual(new ulong[] { 2002 }, api.GetUserLogins("!real*,*"));

        var positions = api.GetPositions(1001);
        Assert.AreEqual(2, positions.Count);
        var p1 = positions.Single(p => p.PositionId == 501);
        Assert.AreEqual(0.10m, p1.Volume);
        Assert.AreEqual(2400.5m, p1.PriceOpen);
        Assert.AreEqual(2401.0m, p1.PriceCurrent);
        Assert.AreEqual(5.0m, p1.Profit);
        Assert.AreEqual(-0.2m, p1.Storage);
        Assert.AreEqual(1_789_000_000_000L, p1.TimeMsc);
        Assert.AreEqual(0u, p1.Action);
        Assert.AreEqual(0, api.GetPositions(2002).Count);

        var a1 = api.GetUserAccount(1001)!;
        Assert.AreEqual("real\\A-Book", a1.Group);
        Assert.AreEqual(100u, a1.Leverage);
        Assert.AreEqual(10_000m, a1.Balance);
        Assert.AreEqual(500m, a1.Credit);
        Assert.AreEqual(10_552.0m, a1.Equity);
        Assert.AreEqual(120.0m, a1.Margin);
        Assert.AreEqual(10_432.0m, a1.FreeMargin);
        Assert.AreEqual("Alice", a1.Name);
        Assert.AreEqual("EUR", api.GetUserAccount(2002)!.Currency);
        Assert.IsNull(api.GetUserAccount(3003));

        var tick = api.GetTickLast("XAUUSD")!;
        Assert.AreEqual(2400.9m, tick.Bid);
        Assert.AreEqual(2401.3m, tick.Ask);
        Assert.AreEqual(1_789_000_000_123L, tick.TimeMsc);
        Assert.IsNull(api.GetTickLast("EURUSD"));

        Assert.AreEqual(0, api.RequestDeals(1001, DateTimeOffset.UnixEpoch, DateTimeOffset.UtcNow).Count);
        Assert.AreEqual(2, api.GetPositionsCalls);
        Assert.AreEqual(3, api.GetUserLoginsCalls);
        Assert.AreEqual(3, api.GetUserAccountCalls);
        Assert.AreEqual(2, api.TickLastCalls);
        Assert.AreEqual(1, api.RequestDealsCalls);

        // The end frame's sequences are the durable resume point.
        api.Disconnect();
        var saved = JsonDocument.Parse(File.ReadAllText(state)).RootElement;
        Assert.AreEqual(server.Url, saved.GetProperty("url").GetString());
        Assert.AreEqual(100, saved.GetProperty("seq").GetProperty("positions").GetInt64());
        Assert.AreEqual(200, saved.GetProperty("seq").GetProperty("deals").GetInt64());
        Assert.AreEqual(300, saved.GetProperty("seq").GetProperty("accounts").GetInt64());
        Assert.AreEqual(400, saved.GetProperty("seq").GetProperty("ticks").GetInt64());
        Assert.IsFalse(api.IsConnected);
    }

    [TestMethod]
    public async Task LiveRecords_UpdateTheBook_AndRaiseTheSubscribedEvents()
    {
        await using var server = new FakeFeedServer();
        using var api = new LiveBridgeApi(TestOptions(server, TempStatePath()));
        var (connection, _) = await ConnectSnapshotAsync(api, server);
        var events = Captured.Attach(api);

        await connection.RecordAsync("positions", "add", 101, P1);
        await connection.RecordAsync("positions", "update", 102, FeedPayloadJson.Position(501, 1001, "XAUUSD", 0, 0.10m, 2400.5, 2405.0, 45.0, -0.2, 1_789_000_000));
        await connection.RecordAsync("positions", "delete", 103, FeedPayloadJson.Position(501, 1001, "XAUUSD", 0, 0.10m, 2400.5, 2405.0, 45.0, -0.2, 1_789_000_000, closingDeal: 777));
        await connection.RecordAsync("deals", "add", 201, FeedPayloadJson.Deal(777, 1001, "XAUUSD", 1, 1, 0.10m, 2405.0, 45.0, -0.2, -0.7, 1_789_000_500, 9001, 501, "close", fee: -0.05));
        await connection.RecordAsync("accounts", "state", 301, A1);
        await connection.RecordsAsync("ticks",
            Rec("ticks", "tick", 401, FeedPayloadJson.Tick("XAUUSD", 2404.9, 2405.3, 1_789_000_000_500)),
            Rec("ticks", "tick", 402, FeedPayloadJson.Tick("EURUSD", 1.08500, 1.08512, 1_789_000_000_501)));

        await WaitUntilAsync(() => events.Ticks.Count == 2 && events.Deleted.Count == 1 && events.Deals.Count == 1 && events.Users.Count == 1, what: "the live events");

        Assert.AreEqual(1, events.Added.Count);
        Assert.AreEqual(1, events.Updated.Count);
        Assert.AreEqual(2405.0m, events.Updated.Single().PriceCurrent);
        Assert.AreEqual(501UL, events.Deleted.Single().PositionId);
        Assert.AreEqual(0, api.GetPositions(1001).Count);

        var deal = events.Deals.Single();
        Assert.AreEqual(777UL, deal.DealId);
        Assert.AreEqual(1001UL, deal.Login);
        Assert.AreEqual(1u, deal.Action);
        Assert.AreEqual(1u, deal.Entry);
        Assert.AreEqual(1000UL, deal.VolumeRaw);
        Assert.AreEqual(0.10m, deal.VolumeLots);
        Assert.AreEqual(2405.0m, deal.Price);
        Assert.AreEqual(45.0m, deal.Profit);
        Assert.AreEqual(-0.2m, deal.Storage);
        Assert.AreEqual(-0.7m, deal.Commission);
        Assert.AreEqual(-0.05m, deal.Fee);
        Assert.AreEqual(1_789_000_500_000L, deal.TimeMsc);
        Assert.AreEqual(9001UL, deal.OrderId);
        Assert.AreEqual(501UL, deal.PositionId);
        Assert.AreEqual("close", deal.Comment);
        var window = api.RequestDeals(1001, DateTimeOffset.FromUnixTimeSeconds(1_789_000_000), DateTimeOffset.FromUnixTimeSeconds(1_789_001_000));
        Assert.AreEqual(777UL, window.Single().DealId);
        Assert.AreEqual(0, api.RequestDeals(2002, DateTimeOffset.UnixEpoch, DateTimeOffset.UtcNow).Count, "another login");
        Assert.AreEqual(0, api.RequestDeals(1001, DateTimeOffset.FromUnixTimeSeconds(1_789_000_600), DateTimeOffset.UtcNow).Count, "outside the window");

        Assert.AreEqual(10_552.0m, events.Users.Single().Equity);
        Assert.AreEqual(2, events.Ticks.Count);
        Assert.AreEqual(2404.9m, api.GetTickLast("XAUUSD")!.Bid);
        Assert.AreEqual(1.08512m, api.GetTickLast("EURUSD")!.Ask);

        var diagnostics = api.Diagnostics();
        var applied = (IReadOnlyDictionary<string, long>)diagnostics["applied"]!;
        Assert.AreEqual(3, applied["positions"]);
        Assert.AreEqual(1, applied["deals"]);
        Assert.AreEqual(1, applied["accounts"]);
        Assert.AreEqual(2, applied["ticks"]);
        Assert.AreEqual("LiveBridge", diagnostics["provider"]);
    }

    [TestMethod]
    public async Task SequenceRule_IgnoresAtOrBelowTheLast_AndTheSameIdentityIsIdempotent()
    {
        await using var server = new FakeFeedServer();
        using var api = new LiveBridgeApi(TestOptions(server, TempStatePath()));
        var (connection, _) = await ConnectSnapshotAsync(api, server, seqDeals: 200);
        var events = Captured.Attach(api);

        var d1 = FeedPayloadJson.Deal(801, 1001, "XAUUSD", 0, 0, 0.5m, 2400, 0, 0, 0, 1_789_000_000, 1, 1);
        await connection.RecordAsync("deals", "add", 250, d1);
        await connection.RecordAsync("deals", "add", 250, d1);   // the same record twice (a replay boundary)
        await connection.RecordAsync("deals", "add", 240, FeedPayloadJson.Deal(802, 1001, "XAUUSD", 0, 0, 0.5m, 2400, 0, 0, 0, 1_789_000_000, 2, 2));   // at or below the last: ignored
        await connection.RecordAsync("deals", "add", 251, d1);   // same identity, newer sequence, same content: no event
        await connection.RecordAsync("positions", "add", 150, P1);
        await connection.RecordAsync("positions", "update", 150, P1);   // at the last: ignored
        await connection.RecordAsync("positions", "update", 151, P1);   // newer, unchanged: no event
        await connection.RecordAsync("positions", "update", 152, FeedPayloadJson.Position(501, 1001, "XAUUSD", 0, 0.10m, 2400.5, 2410.0, 95.0, -0.2, 1_789_000_000));
        await connection.HeartbeatAsync();

        await WaitUntilAsync(() => api.LastFrameAtUtc is not null && api.Diagnostics()["framesReceived"] is long n && n >= 15, what: "all frames");
        await WaitUntilAsync(() => events.Updated.Count == 1, what: "the one real update");

        Assert.AreEqual(1, events.Deals.Count);
        Assert.AreEqual(801UL, events.Deals.Single().DealId);
        Assert.AreEqual(1, api.RequestDeals(1001, DateTimeOffset.UnixEpoch, DateTimeOffset.UtcNow).Count, "802 was never applied");
        Assert.AreEqual(1, events.Added.Count);
        Assert.AreEqual(1, events.Updated.Count);
        var skipped = (IReadOnlyDictionary<string, long>)api.Diagnostics()["skippedBySequence"]!;
        Assert.AreEqual(2, skipped["deals"]);
        Assert.AreEqual(1, skipped["positions"]);
        var sequences = (IReadOnlyDictionary<string, long>)api.Diagnostics()["sequences"]!;
        Assert.AreEqual(251, sequences["deals"]);
        Assert.AreEqual(152, sequences["positions"]);
    }

    [TestMethod]
    public async Task Restart_ResumesFromTheDurableSequences_AppliesTheReplay_AndAcceptsTheTickGap()
    {
        await using var server = new FakeFeedServer();
        var state = TempStatePath();

        using (var first = new LiveBridgeApi(TestOptions(server, state)))
        {
            await ConnectSnapshotAsync(first, server, positions: new[] { Rec("positions", "add", 90, P1) }, seqPositions: 100, seqDeals: 200, seqAccounts: 300, seqTicks: 400);
            first.Disconnect();
        }

        using var api = new LiveBridgeApi(TestOptions(server, state));
        Assert.IsTrue(api.Initialize(), api.LastError);
        var events = Captured.Attach(api);   // a consumer may attach handlers before Connect; the replay must reach them
        var connect = Task.Run(() => api.Connect("mt5.example:443", 1065, "unused", 10_000));
        var connection = await server.NextConnectionAsync();
        var subscribe = await connection.Subscribe;
        var resume = subscribe.GetProperty("resume");
        Assert.AreEqual(100, resume.GetProperty("positions").GetInt64());
        Assert.AreEqual(200, resume.GetProperty("deals").GetInt64());
        Assert.AreEqual(300, resume.GetProperty("accounts").GetInt64());
        Assert.AreEqual(400, resume.GetProperty("ticks").GetInt64());

        await connection.HelloAsync("resume");
        await connection.ReplayAsync("positions",
            Rec("positions", "update", 150, FeedPayloadJson.Position(501, 1001, "XAUUSD", 0, 0.10m, 2400.5, 2410.0, 95.0, -0.2, 1_789_000_000)),
            Rec("positions", "delete", 160, FeedPayloadJson.Position(502, 1001, "EURUSD", 1, 1.00m, 1.08547, 1.08500, 47.0, 0, 1_789_000_100, closingDeal: 778)));
        await connection.ReplayAsync("deals",
            Rec("deals", "add", 260, FeedPayloadJson.Deal(778, 1001, "EURUSD", 0, 1, 1.00m, 1.085, 47.0, 0, -7, 1_789_000_700, 9002, 502)));
        await connection.ReplayAsync("accounts", Rec("accounts", "state", 350, A1));
        await connection.ReplayGapAsync("ticks", "beyond the buffer");
        await connection.SnapshotAsync("ticks", Rec("ticks", "tick", 0, T1));
        await connection.ReplayEndAsync(160, 260, 350, 450);
        Assert.IsTrue(await connect, api.LastError);

        Assert.AreEqual("resume", api.Mode);
        Assert.AreEqual(2410.0m, api.GetPositions(1001).Single().PriceCurrent);
        Assert.AreEqual(778UL, api.RequestDeals(1001, DateTimeOffset.UnixEpoch, DateTimeOffset.UtcNow).Single().DealId);
        Assert.AreEqual(10_552.0m, api.GetUserAccount(1001)!.Equity);
        Assert.AreEqual(2400.9m, api.GetTickLast("XAUUSD")!.Bid);
        Assert.AreEqual(1, api.TickGaps);
        Assert.AreEqual(0, api.DealGaps);
        Assert.AreEqual(1, events.Deals.Count, "the replayed deal reached the handler");
        // A fresh process has an empty book: the replayed position "update" (its current state) is learned as an add,
        // and the replayed "delete" of a position never seen is still raised so a consumer can drop it.
        Assert.AreEqual(1, events.Added.Count);
        Assert.AreEqual(0, events.Updated.Count);
        Assert.AreEqual(1, events.Deleted.Count);
        Assert.AreEqual(1, events.Ticks.Count);
        var sequences = (IReadOnlyDictionary<string, long>)api.Diagnostics()["sequences"]!;
        Assert.AreEqual(450, sequences["ticks"]);
        Assert.AreEqual(260, sequences["deals"]);
    }

    [TestMethod]
    public async Task Bye_ReconnectsWithResume_AndTheReplayedDealsReachTheHandlers()
    {
        await using var server = new FakeFeedServer();
        using var api = new LiveBridgeApi(TestOptions(server, TempStatePath()));
        var (connection, _) = await ConnectSnapshotAsync(api, server);
        var events = Captured.Attach(api);

        await connection.RecordAsync("deals", "add", 201, FeedPayloadJson.Deal(801, 1001, "XAUUSD", 0, 0, 0.5m, 2400, 0, 0, 0, 1_789_000_000, 1, 1));
        await WaitUntilAsync(() => events.Deals.Count == 1, what: "the first deal");

        await connection.ByeAsync("too slow: 100000 behind");
        await WaitUntilAsync(() => !api.IsConnected, what: "the drop");

        var again = await server.NextConnectionAsync();
        var subscribe = await again.Subscribe;
        var resume = subscribe.GetProperty("resume");
        Assert.AreEqual(100, resume.GetProperty("positions").GetInt64());
        Assert.AreEqual(201, resume.GetProperty("deals").GetInt64());
        Assert.AreEqual(300, resume.GetProperty("accounts").GetInt64());
        Assert.AreEqual(400, resume.GetProperty("ticks").GetInt64());

        await again.HelloAsync("resume");
        await again.ReplayAsync("positions");
        await again.ReplayAsync("deals", Rec("deals", "add", 202, FeedPayloadJson.Deal(802, 1001, "XAUUSD", 1, 1, 0.5m, 2401, 50, 0, 0, 1_789_000_010, 2, 1)));
        await again.ReplayAsync("accounts");
        await again.ReplayAsync("ticks");
        await again.ReplayEndAsync(100, 202, 300, 400);

        await WaitUntilAsync(() => api.IsConnected, what: "the reconnect");
        await WaitUntilAsync(() => events.Deals.Count == 2, what: "the replayed deal");
        Assert.AreEqual(802UL, events.Deals.Last().DealId);
        Assert.AreEqual("too slow: 100000 behind", api.LastByeReason);
        Assert.AreEqual(1, api.Reconnects);
        Assert.AreEqual(2, api.Handshakes);
        Assert.AreEqual(2, server.Accepted);
        Assert.IsTrue(await Task.WhenAny(connection.Closed, Task.Delay(3000)) == connection.Closed, "the first socket completed its close");
    }

    [TestMethod]
    public async Task Silence_ReconnectsAfterTheLimit_WhileHeartbeatsKeepTheLineAlive()
    {
        await using var server = new FakeFeedServer();
        var options = TestOptions(server, TempStatePath());
        options.SilenceLimitMs = 1000;
        using var api = new LiveBridgeApi(options);
        var (connection, _) = await ConnectSnapshotAsync(api, server);

        // Heartbeats inside the limit: no reconnect.
        for (var i = 0; i < 6; i++)
        {
            await connection.HeartbeatAsync("connected");
            await Task.Delay(250);
        }
        Assert.AreEqual(1, server.Accepted);
        Assert.IsTrue(api.IsConnected);
        Assert.AreEqual(true, api.SourceConnected);

        // Then nothing at all: the line is dead after the limit and the consumer dials again with resume.
        var again = await server.NextConnectionAsync(4000);
        var subscribe = await again.Subscribe;
        Assert.AreEqual(200, subscribe.GetProperty("resume").GetProperty("deals").GetInt64());
        Assert.AreEqual(1, api.Reconnects);
        StringAssert.Contains(api.LastDropReason, "silent");
    }

    [TestMethod]
    public async Task ARefusedKey_FailsConnectWithTheHttpStatus_AndAnEmptyKeyFailsWithoutDialing()
    {
        await using var server = new FakeFeedServer(requiredKey: "another-key");

        using (var wrong = new LiveBridgeApi(TestOptions(server, TempStatePath())))
        {
            Assert.IsTrue(wrong.Initialize());
            Assert.IsFalse(wrong.Connect("srv", 1, "pw", 5000));
            StringAssert.Contains(wrong.LastError, "401");
            StringAssert.Contains(wrong.LastError, "LiveBridge__ApiKey");
            Assert.IsFalse(wrong.IsConnected);
            Assert.AreEqual("disconnected", wrong.State);
        }
        Assert.AreEqual(1, server.Refused);

        using (var empty = new LiveBridgeApi(TestOptions(server, TempStatePath(), key: "")))
        {
            Assert.IsTrue(empty.Initialize());
            Assert.IsFalse(empty.Connect("srv", 1, "pw", 5000));
            StringAssert.Contains(empty.LastError, "LiveBridge__ApiKey");
        }
        await Task.Delay(200);
        Assert.AreEqual(1, server.Refused, "an empty key never dials");
        Assert.AreEqual(0, server.Accepted);

        using var notInitialized = new LiveBridgeApi(TestOptions(server, TempStatePath()));
        Assert.IsFalse(notInitialized.Connect("srv", 1, "pw"));
        StringAssert.Contains(notInitialized.LastError, "Initialize");
    }

    [TestMethod]
    public async Task ASnapshotAfterAReconnect_ClosesThePositionsMissingFromIt()
    {
        await using var server = new FakeFeedServer();
        using var api = new LiveBridgeApi(TestOptions(server, TempStatePath()));
        var (connection, _) = await ConnectSnapshotAsync(api, server, positions: new[] { Rec("positions", "add", 90, P1), Rec("positions", "add", 95, P2) });
        var events = Captured.Attach(api);
        Assert.AreEqual(2, api.GetPositions(1001).Count);

        await connection.ByeAsync("the bridge restarted");
        var again = await server.NextConnectionAsync();
        await again.Subscribe;
        // The bridge answers a resume with a snapshot (its store is younger than our sequences): full state, P2 is gone.
        await again.SnapshotHandshakeAsync(Array.Empty<string>(), new[] { Rec("positions", "add", 96, P1) }, Array.Empty<string>(), 500, 600, 700, 800);

        await WaitUntilAsync(() => api.IsConnected && events.Deleted.Count == 1, what: "the reconciliation");
        Assert.AreEqual(502UL, events.Deleted.Single().PositionId);
        Assert.AreEqual(0, events.Added.Count, "P1 unchanged: no add event");
        CollectionAssert.AreEqual(new ulong[] { 501 }, api.GetPositions(1001).Select(p => p.PositionId).ToArray());
        Assert.AreEqual("snapshot", api.Mode);
        var sequences = (IReadOnlyDictionary<string, long>)api.Diagnostics()["sequences"]!;
        Assert.AreEqual(500, sequences["positions"]);
    }

    [TestMethod]
    public async Task Acks_CarryTheAppliedSequences()
    {
        await using var server = new FakeFeedServer();
        var options = TestOptions(server, TempStatePath());
        options.AckEveryMs = 150;
        using var api = new LiveBridgeApi(options);
        var (connection, _) = await ConnectSnapshotAsync(api, server);
        await connection.RecordAsync("deals", "add", 201, FeedPayloadJson.Deal(801, 1001, "XAUUSD", 0, 0, 0.5m, 2400, 0, 0, 0, 1_789_000_000, 1, 1));

        await WaitUntilAsync(() => connection.Acks.Any(a => a.GetProperty("seq").TryGetProperty("deals", out var d) && d.GetInt64() == 201), what: "an ack with the deal sequence");
        var ack = connection.Acks.Last(a => a.GetProperty("seq").GetProperty("deals").GetInt64() == 201);
        Assert.AreEqual("ack", ack.GetProperty("type").GetString());
        Assert.AreEqual(100, ack.GetProperty("seq").GetProperty("positions").GetInt64());
        Assert.AreEqual(400, ack.GetProperty("seq").GetProperty("ticks").GetInt64());
    }

    [TestMethod]
    public async Task AnUncomputedAccount_FallsBackToBalancePlusCredit()
    {
        await using var server = new FakeFeedServer();
        using var api = new LiveBridgeApi(TestOptions(server, TempStatePath()));
        var pushedOnly = FeedPayloadJson.Account(3003, "real\\C", 200, "USD", 2_500, 100, null, null, null, null, computed: false);
        await ConnectSnapshotAsync(api, server, accounts: new[] { Rec("accounts", "state", 0, pushedOnly) });

        var account = api.GetUserAccount(3003)!;
        Assert.AreEqual(2_600m, account.Equity);
        Assert.AreEqual(0m, account.Margin);
        Assert.AreEqual(2_600m, account.FreeMargin);
        Assert.AreEqual("", account.Name);
        Assert.AreEqual(0L, account.RegistrationTime);
    }

    [TestMethod]
    public async Task Disconnect_ClosesGracefully_FlushesTheSequences_AndStopsReconnecting()
    {
        await using var server = new FakeFeedServer();
        var state = TempStatePath();
        var api = new LiveBridgeApi(TestOptions(server, state));
        var (connection, _) = await ConnectSnapshotAsync(api, server);
        await connection.RecordAsync("deals", "add", 201, FeedPayloadJson.Deal(801, 1001, "XAUUSD", 0, 0, 0.5m, 2400, 0, 0, 0, 1_789_000_000, 1, 1));
        await WaitUntilAsync(() => api.RequestDeals(1001, DateTimeOffset.UnixEpoch, DateTimeOffset.UtcNow).Count == 1);

        api.Disconnect();
        Assert.IsFalse(api.IsConnected);
        Assert.AreEqual("disconnected", api.State);
        Assert.IsTrue(await Task.WhenAny(connection.Closed, Task.Delay(3000)) == connection.Closed, "the consumer's close reached the bridge");
        Assert.AreEqual(201, JsonDocument.Parse(File.ReadAllText(state)).RootElement.GetProperty("seq").GetProperty("deals").GetInt64());

        await Task.Delay(600);
        Assert.AreEqual(1, server.Accepted, "no reconnect after a deliberate disconnect");

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
    public void Factory_DefaultsToManager_WhichNeedsTheManagerAccount()
    {
        var factory = new MT5ApiFactory();
        Assert.AreEqual(MT5ApiProviders.Manager, factory.ProviderName);
        Assert.IsTrue(factory.RequiresManagerAccount, "the Manager API takes its credentials from account_settings");
        Assert.IsNull(factory.Endpoint);
    }

    [TestMethod]
    public void Factory_LiveBridge_NeedsNoManagerAccount_AndNamesTheFeedAsEndpoint()
    {
        var factory = new MT5ApiFactory("LiveBridge", new LiveBridgeOptions { Url = "wss://feed.example:5571/feed/src" });
        Assert.IsFalse(factory.RequiresManagerAccount, "the feed is keyed by URL + bearer key; the bring-up must not wait for account_settings");
        Assert.AreEqual("wss://feed.example:5571/feed/src", factory.Endpoint);
    }

    [TestMethod]
    public void Factory_LiveBridge_CreatesTheFeedAdapter_WhichFailsFastWithoutAKey()
    {
        var factory = new MT5ApiFactory("LiveBridge", new LiveBridgeOptions { Url = "wss://feed.example:5571/feed/x", ApiKey = "" });
        Assert.AreEqual(MT5ApiProviders.LiveBridge, factory.ProviderName);

        using var api = factory.Create();
        Assert.IsInstanceOfType(api, typeof(LiveBridgeApi));
        Assert.IsInstanceOfType(api, typeof(IMT5ApiDiagnostics));
        Assert.IsTrue(api.Initialize());
        Assert.IsFalse(api.Connect("srv", 1, "pw"));
        StringAssert.Contains(api.LastError, "LiveBridge__ApiKey");
        Assert.IsFalse(api.LastError.Contains("pw"), "the password is never echoed");
    }

    [TestMethod]
    public void Factory_UnknownProvider_FailsAtConstruction()
    {
        Assert.ThrowsException<ArgumentException>(() => new MT5ApiFactory("nope"));
    }
}
