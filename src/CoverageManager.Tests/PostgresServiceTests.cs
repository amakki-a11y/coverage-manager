using CoverageManager.Api.Services;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;
using CoverageManager.Core.Models.Bridge;
using CoverageManager.Core.Models.EquityPnL;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CoverageManager.Tests;

/// <summary>
/// Integration tests for the v2 <see cref="PostgresService"/> against a real local
/// PostgreSQL. Proves feed-shaped records write and read back and that the settled /
/// full aggregation SQL functions return correct results on seeded synthetic data.
///
/// Self-contained: creates a uniquely-named throwaway database, applies every
/// db/migrations/*.sql, runs the tests, and drops the database. Requires a local
/// Postgres superuser password in env var <c>CMV2_TEST_PG_PW</c> (host/port/user
/// default to 127.0.0.1:5432/postgres, migrations dir auto-located from the source
/// tree or <c>CMV2_MIGRATIONS_DIR</c>). If the env var is absent the whole class is
/// skipped (Inconclusive) so CI without Postgres stays green.
///
/// NOTE: touches ONLY its throwaway database -- never coverage_v2, never any feed.
/// </summary>
[TestClass]
public class PostgresServiceTests
{
    private static string _host = "127.0.0.1";
    private static int _port = 5432;
    private static string _user = "postgres";
    private static string? _pw;
    private static string _dbName = "cmv2_apptest_" + Guid.NewGuid().ToString("N")[..8];
    private static PostgresService _svc = null!;
    private static bool _enabled;

    private static string AdminCs => $"Host={_host};Port={_port};Database=postgres;Username={_user};Password={_pw}";
    private static string AppCs   => $"Host={_host};Port={_port};Database={_dbName};Username={_user};Password={_pw}";

    private static readonly DateTime WinFrom = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WinTo   = new(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [ClassInitialize]
    public static async Task Init(TestContext _)
    {
        _pw = Environment.GetEnvironmentVariable("CMV2_TEST_PG_PW");
        _host = Environment.GetEnvironmentVariable("CMV2_TEST_PG_HOST") ?? _host;
        if (int.TryParse(Environment.GetEnvironmentVariable("CMV2_TEST_PG_PORT"), out var p)) _port = p;
        _user = Environment.GetEnvironmentVariable("CMV2_TEST_PG_USER") ?? _user;
        _enabled = !string.IsNullOrWhiteSpace(_pw);
        if (!_enabled) return;

        // create throwaway db
        await using (var admin = new NpgsqlConnection(AdminCs))
        {
            await admin.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE {_dbName}", admin);
            await cmd.ExecuteNonQueryAsync();
        }

        // apply migrations
        var migDir = Environment.GetEnvironmentVariable("CMV2_MIGRATIONS_DIR") ?? LocateMigrationsDir();
        await using (var c = new NpgsqlConnection(AppCs))
        {
            await c.OpenAsync();
            foreach (var file in Directory.GetFiles(migDir, "*.sql").OrderBy(f => f))
            {
                var sql = await File.ReadAllTextAsync(file);
                await using var cmd = new NpgsqlCommand(sql, c);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Postgres:ConnectionString"] = AppCs
        }).Build();
        _svc = new PostgresService(config, NullLogger<PostgresService>.Instance);
    }

    [ClassCleanup]
    public static async Task Cleanup()
    {
        if (!_enabled) return;
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(AdminCs);
        await admin.OpenAsync();
        await using var term = new NpgsqlCommand(
            $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='{_dbName}' AND pid<>pg_backend_pid()", admin);
        await term.ExecuteNonQueryAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {_dbName}", admin);
        await drop.ExecuteNonQueryAsync();
    }

    private static string LocateMigrationsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "db", "migrations");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate db/migrations from " + AppContext.BaseDirectory);
    }

    private static void RequireEnabled()
    {
        if (!_enabled) Assert.Inconclusive("CMV2_TEST_PG_PW not set — skipping Postgres integration tests.");
    }

    [TestInitialize]
    public async Task ResetTables()
    {
        if (!_enabled) return;
        await using var c = new NpgsqlConnection(AppCs);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "TRUNCATE deals, symbol_mappings, trading_accounts, exposure_snapshots, equity_pnl_client_config, bridge_executions, trade_audit_log, reconciliation_runs, retention_prune_runs RESTART IDENTITY CASCADE", c);
        await cmd.ExecuteNonQueryAsync();
    }

    // ---- feed-shaped deal fixture (mirrors the Phase 0 import self-test) ----
    private static List<DealRecord> SeedDeals() => new()
    {
        new() { Source="bbook", DealId=1001, Login=5001, Symbol="XAUUSD-", CanonicalSymbol="XAUUSD",
                Direction="SELL", Action=1, Entry=1, Volume=1, Price=2400.5m, Profit=100, Commission=-5, Swap=-2, Fee=-1,
                DealTime=new DateTime(2026,4,2,10,0,0,DateTimeKind.Utc) },
        new() { Source="bbook", DealId=1002, Login=5001, Symbol="XAUUSD-", CanonicalSymbol="XAUUSD",
                Direction="BUY", Action=0, Entry=0, Volume=1, Price=2399m, Profit=0, Commission=-5, Swap=0, Fee=0,
                DealTime=new DateTime(2026,4,1,10,0,0,DateTimeKind.Utc) },
        new() { Source="bbook", DealId=1003, Login=5002, Symbol="XAUUSD.c", CanonicalSymbol="XAUUSD.c",
                Direction="SELL", Action=1, Entry=1, Volume=1, Price=2401m, Profit=50, Commission=-2, Swap=0, Fee=0,
                DealTime=new DateTime(2026,4,3,10,0,0,DateTimeKind.Utc) },
        new() { Source="bbook", DealId=1004, Login=5001, Symbol="", CanonicalSymbol="",
                Direction="BALANCE", Action=2, Entry=0, Volume=0, Price=0, Profit=1000, Commission=0, Swap=0, Fee=0,
                DealTime=new DateTime(2026,4,4,10,0,0,DateTimeKind.Utc) },
    };

    [TestMethod]
    public async Task Deals_WriteReadBack_And_Aggregations()
    {
        RequireEnabled();
        var written = await _svc.UpsertDealsAsync(SeedDeals());
        Assert.AreEqual(4, written, "should upsert 4 feed-shaped deals");

        // read back
        var read = await _svc.GetDealsAsync("bbook", WinFrom, WinTo);
        Assert.AreEqual(4, read.Count, "should read back all 4 deals");
        var d1 = read.Single(d => d.DealId == 1001);
        Assert.AreEqual(100m, d1.Profit);
        Assert.AreEqual("XAUUSD", d1.CanonicalSymbol);
        Assert.AreEqual(DateTimeKind.Utc, d1.DealTime.Kind, "timestamptz must read back as UTC");

        // settled: 92 (OUT) - 5 (IN comm) + 48 (.c OUT, canonical-merged) = 135; BALANCE excluded
        var settled = await _svc.AggregateBBookSettledPnlAsync(WinFrom, WinTo, Array.Empty<long>());
        Assert.IsTrue(settled.ContainsKey("XAUUSD"), "settled must key on normalized canonical XAUUSD");
        Assert.AreEqual(135m, settled["XAUUSD"], "settled(XAUUSD) with .c merge, IN excl profit, BALANCE excluded");

        // full: volume includes IN+OUT trade deals (3), buy=1, sell=2
        var full = await _svc.AggregateBBookPnLFullAsync(WinFrom, WinTo, Array.Empty<long>());
        var x = full.Single(s => s.Symbol == "XAUUSD");
        Assert.AreEqual(3m, x.TotalVolume);
        Assert.AreEqual(1m, x.BuyVolume);
        Assert.AreEqual(2m, x.SellVolume);
        Assert.AreEqual(3, x.DealCount, "3 trade deals (BALANCE excluded)");

        // excluded_logins removes 5002's .c deal from the XAUUSD settled
        var settledEx = await _svc.AggregateBBookSettledPnlAsync(WinFrom, WinTo, new long[] { 5002 });
        Assert.AreEqual(87m, settledEx["XAUUSD"], "excluding login 5002 drops the +48 .c deal -> 135-48=87");
    }

    [TestMethod]
    public async Task Deals_Upsert_Idempotent_And_LastTime_And_BalanceFlow()
    {
        RequireEnabled();
        await _svc.UpsertDealsAsync(SeedDeals());
        // re-upsert with a changed profit; must UPDATE not duplicate
        var again = SeedDeals();
        again.Single(d => d.DealId == 1001).Profit = 111m;
        await _svc.UpsertDealsAsync(again);

        var read = await _svc.GetDealsAsync("bbook", WinFrom, WinTo);
        Assert.AreEqual(4, read.Count, "re-upsert must not duplicate (conflict on source,deal_id)");
        Assert.AreEqual(111m, read.Single(d => d.DealId == 1001).Profit, "conflict must UPDATE the row");

        var last = await _svc.GetLastDealTimeAsync("bbook");
        Assert.AreEqual(new DateTime(2026,4,4,10,0,0,DateTimeKind.Utc), last!.Value.ToUniversalTime());

        // trade-flow excludes action 2/3; login 5001 trade deals: 1001(profit111,comm-5,swap-2,fee-1)=103, 1002(comm-5)=-5 => 98
        var flow = await _svc.SumTradeBalanceFlowPerLoginAsync("bbook", WinFrom, WinTo);
        Assert.AreEqual(98m, flow[5001], "trade-flow for 5001 excludes the BALANCE deal");
        Assert.IsFalse(flow.ContainsKey(0), "no phantom login from BALANCE row");
    }

    [TestMethod]
    public async Task Mappings_Accounts_Snapshots_RoundTrip()
    {
        RequireEnabled();
        var m = new SymbolMapping { CanonicalName="XAUUSD", BBookSymbol="XAUUSD-", BBookContractSize=100,
            CoverageSymbol="XAUUSD.c", CoverageContractSize=100, Digits=2, PipSize=0.01m, IsActive=true };
        var savedM = await _svc.UpsertMappingAsync(m);
        Assert.IsNotNull(savedM);
        var mappings = await _svc.GetMappingsAsync();
        Assert.IsTrue(mappings.Any(x => x.BBookSymbol=="XAUUSD-" && x.PipSize==0.01m));

        var acct = new TradingAccount { Source="bbook", Login=5001, Name="Alice", GroupName="real\\A",
            Balance=10000, Equity=10250, Credit=0, Margin=500, FreeMargin=9750, Currency="USD", Status="active" };
        Assert.AreEqual(1, await _svc.UpsertTradingAccountsAsync(new[] { acct }));
        var accts = await _svc.GetTradingAccountsAsync("bbook");
        Assert.AreEqual(10250m, accts.Single(a => a.Login==5001).Equity);

        var snap = new ExposureSnapshot { CanonicalSymbol="XAUUSD",
            SnapshotTime=new DateTime(2026,3,27,22,0,0,DateTimeKind.Utc), NetVolume=5, NetPnL=2300.5m };
        Assert.AreEqual(1, await _svc.UpsertExposureSnapshotsAsync(new[] { snap }));
        var nearest = await _svc.GetNearestSnapshotsBeforeAsync(new DateTime(2026,4,1,0,0,0,DateTimeKind.Utc));
        Assert.IsTrue(nearest.ContainsKey("XAUUSD"));
        Assert.AreEqual(2300.5m, nearest["XAUUSD"].NetPnL);
    }

    [TestMethod]
    public async Task BridgeExecutions_RoundTrip_AndReadsV1ShapedCovFills()
    {
        RequireEnabled();

        // 1. write/read a pair through the store (the bridge_executions port)
        var pair = new ExecutionPair
        {
            ClientDealId = "EXID-1", CenOrdId = "ORD-1", Symbol = "XAUUSD", Side = BridgeSide.SELL,
            ClientVolume = 2m, ClientPrice = 2400.5m,
            ClientTimeUtc = new DateTime(2026, 4, 17, 1, 41, 38, DateTimeKind.Utc),
            ClientMtLogin = 5001UL, ClientMtTicket = 21365693UL, ClientMtDealId = 284872UL,
            CovVolume = 2m, AvgCovPrice = 2400.4m, PriceEdge = 0.1m, Pips = 10m,
            MaxTimeDiffMs = 120, MinTimeDiffMs = 0,
            CovFills = new List<CovFill>
            {
                new() { DealId = "mk|284872", Volume = 2m, Price = 4773.69m,
                        TimeUtc = new DateTime(2026,4,17,1,41,38,DateTimeKind.Utc),
                        TimeDiffMs = 0, LpName = "FXGROW_OZ_LIVE", MtTicket = 21365693UL,
                        RawPrice = 4773.68m, ExtMarkup = -0.02m }
            }
        };
        Assert.AreEqual(1, await _svc.UpsertBridgeExecutionsAsync(new[] { pair }));

        var back = await _svc.QueryBridgeExecutionsAsync(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), WinTo, null, 100);
        var got = back.Single(p => p.ClientDealId == "EXID-1");
        Assert.AreEqual(BridgeSide.SELL, got.Side);
        Assert.AreEqual(284872UL, got.ClientMtDealId);
        Assert.AreEqual(1m, got.CoverageRatio, "generated column: cov_volume / client_volume");
        Assert.AreEqual(1, got.CovFills.Count, "cov_fills jsonb must round-trip");
        Assert.AreEqual("FXGROW_OZ_LIVE", got.CovFills[0].LpName);
        Assert.AreEqual(4773.68m, got.CovFills[0].RawPrice);

        // 2. a row shaped exactly like v1's (snake_case cov_fills keys written by the old
        //    Supabase writer) must still deserialize -- otherwise v2 silently loses the
        //    coverage legs of every imported historical pair.
        await using (var c = new NpgsqlConnection(AppCs))
        {
            await c.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
INSERT INTO bridge_executions (client_deal_id, cen_ord_id, symbol, side, client_volume, client_price,
    client_time, client_mt_deal_id, cov_volume, cov_fills)
VALUES ('V1-ROW','ORD-V1','US30','BUY',1,40000,'2026-04-17T01:41:38Z',999,1,
 '[{""price"": 4773.69, ""volume"": 2, ""deal_id"": ""mk|284872"", ""lp_name"": ""FXGROW_OZ_LIVE"",
    ""time_utc"": ""2026-04-17T01:41:38.48417Z"", ""mt_ticket"": 21365693, ""raw_price"": 4773.68,
    ""ext_markup"": -0.02, ""time_diff_ms"": 0}]'::jsonb);", c);
            await cmd.ExecuteNonQueryAsync();
        }

        var v1 = (await _svc.QueryBridgeExecutionsAsync(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), WinTo, "US30", 100))
            .Single(p => p.ClientDealId == "V1-ROW");
        Assert.AreEqual(1, v1.CovFills.Count, "v1-shaped snake_case cov_fills must deserialize");
        Assert.AreEqual("mk|284872", v1.CovFills[0].DealId);
        Assert.AreEqual(21365693UL, v1.CovFills[0].MtTicket);
        Assert.AreEqual(-0.02m, v1.CovFills[0].ExtMarkup);
    }

    // ---- feed/store self-check: injected divergence against a real store ----

    private sealed class FakeFeed : IFeedDealSource
    {
        public bool IsConnected { get; set; } = true;
        public DealHistoryWindow DealHistory { get; set; }
        public List<ClosedDeal> Deals { get; } = new();
        public IReadOnlyList<ClosedDeal> QueryDeals(DateTimeOffset from, DateTimeOffset to) =>
            Deals.Where(d => d.Time >= from.UtcDateTime && d.Time < to.UtcDateTime).ToList();
    }

    private static ClosedDeal FeedDeal(ulong id, decimal profit, DateTime at) => new()
    {
        DealId = id, Login = 5001, Symbol = "XAUUSD-", Direction = "SELL", VolumeLots = 1m, Price = 2400m,
        Profit = profit, Commission = -1m, Swap = 0m, Fee = 0m, Entry = 1, Action = 1, Time = at,
    };

    private static DealRecord StoreDeal(long id, decimal profit, DateTime at, int action = 1, string canonical = "XAUUSD") => new()
    {
        Source = "bbook", DealId = id, Login = 5001, Symbol = "XAUUSD-", CanonicalSymbol = canonical,
        Direction = "SELL", Action = action, Entry = 1, Volume = 1m, Price = 2400m,
        Profit = profit, Commission = -1m, Swap = 0m, Fee = 0m, DealTime = at,
    };

    [TestMethod]
    public async Task SelfCheck_InjectedDivergence_IsRewritten_AndNothingIsEverDeleted()
    {
        RequireEnabled();
        // Whole seconds so a timestamptz round trip compares exactly.
        var nowS = DateTime.UtcNow;
        var t = new DateTime(nowS.Year, nowS.Month, nowS.Day, nowS.Hour, nowS.Minute, nowS.Second, DateTimeKind.Utc).AddHours(-2);

        // STORE (what persistence left behind)
        await _svc.UpsertDealsAsync(new[]
        {
            StoreDeal(7001, 10m, t),                                   // A: identical to the feed
            StoreDeal(7002, 10m, t.AddMinutes(1), canonical: "LEGACY-KEY"), // B: profit diverged; odd canonical must survive
            StoreDeal(7003, 10m, t.AddMinutes(2)),                     // C: store-only, inside the window
            StoreDeal(7004, 10m, new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc)), // D: store-only, older than the window
            StoreDeal(7005, 500m, t.AddMinutes(3), action: 2),         // X: a cash movement the feed query never returns
        });

        // FEED (the authority's working set)
        var feed = new FakeFeed { DealHistory = DealHistoryWindow.Since(t.AddHours(-1)) };
        feed.Deals.Add(FeedDeal(7001, 10m, t));                 // A same
        feed.Deals.Add(FeedDeal(7002, 99m, t.AddMinutes(1)));   // B corrected value
        feed.Deals.Add(FeedDeal(7006, 42m, t.AddMinutes(4)));   // E never reached the store

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SelfCheck:IntervalMinutes"] = "15", ["SelfCheck:StartupDelayMinutes"] = "0",
        }).Build();
        var selfCheck = new FeedStoreSelfCheckService(feed, _svc, new CoverageManager.Core.Engines.PositionManager(),
            config, NullLogger<FeedStoreSelfCheckService>.Instance);

        var run = await selfCheck.RunNowAsync("manual");

        Assert.IsNull(run.Error, run.Error);
        Assert.AreEqual(1, run.Backfilled, "E re-written from the feed");
        Assert.AreEqual(1, run.Modified, "B corrected from the feed");
        Assert.AreEqual(0, run.GhostDeleted, "the self-check never deletes");
        Assert.AreEqual(3, run.Mt5DealCount, "feed trade deals in window");
        Assert.AreEqual(3, run.SupabaseDealCount, "stored trade deals in window: A, B, C");
        StringAssert.Contains(run.Notes, "1 store-only kept (never deleted)");

        var stored = await _svc.GetDealsAsync("bbook", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), DateTime.UtcNow.AddDays(1));
        var byId = stored.ToDictionary(d => d.DealId);
        Assert.IsTrue(byId.ContainsKey(7006), "missing deal re-written");
        Assert.AreEqual(99m, byId[7002].Profit, "diverged value corrected");
        Assert.AreEqual("LEGACY-KEY", byId[7002].CanonicalSymbol, "correction heals values, never re-keys");
        Assert.IsTrue(byId.ContainsKey(7003), "NO-DELETE: store-only deal inside the window survives");
        Assert.IsTrue(byId.ContainsKey(7004), "NO-DELETE: store-only deal older than the window survives");
        Assert.IsTrue(byId.ContainsKey(7005), "NO-DELETE: cash movement survives");
        Assert.AreEqual(6, stored.Count);

        var audit = await _svc.GetAuditLogAsync(login: 5001);
        Assert.IsTrue(audit.Any(a => a.DealId == 7002 && a.FieldChanged == "profit" && a.NewValue == "99.00"),
            "the corrected value is recorded in trade_audit_log");

        var runs = await _svc.ListReconciliationRunsAsync(5);
        Assert.IsTrue(runs.Any(r => r.Backfilled == 1 && r.Modified == 1 && r.GhostDeleted == 0),
            "the run is recorded where the Settings card reads it");

        // Converges: a second pass finds nothing left to do and still deletes nothing.
        var again = await selfCheck.RunNowAsync("manual");
        Assert.AreEqual(0, again.Backfilled);
        Assert.AreEqual(0, again.Modified);
        Assert.AreEqual(6, (await _svc.GetDealsAsync("bbook", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), DateTime.UtcNow.AddDays(1))).Count);
    }

    [TestMethod]
    public async Task SelfCheck_FeedDisconnected_RecordsASkip_AndTouchesNothing()
    {
        RequireEnabled();
        var t = DateTime.UtcNow.AddHours(-1);
        await _svc.UpsertDealsAsync(new[] { StoreDeal(8001, 10m, t) });
        var feed = new FakeFeed { IsConnected = false, DealHistory = DealHistoryWindow.Since(t.AddHours(-1)) };
        var selfCheck = new FeedStoreSelfCheckService(feed, _svc, new CoverageManager.Core.Engines.PositionManager(),
            new ConfigurationBuilder().Build(), NullLogger<FeedStoreSelfCheckService>.Instance);

        var run = await selfCheck.RunNowAsync("scheduled");

        Assert.AreEqual("feed not connected", run.Error);
        Assert.AreEqual(0, run.Backfilled + run.Modified + run.GhostDeleted);
        Assert.AreEqual(1, (await _svc.GetDealsAsync("bbook", t.AddHours(-1), DateTime.UtcNow.AddDays(1))).Count);
    }

    // ---- 12-month retention pruner + Postgres-backed history ----

    private static DealRetentionPruneService Pruner(int months, int batchSize = 100) =>
        new(_svc, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Retention:Months"] = months.ToString(), ["Retention:BatchSize"] = batchSize.ToString(),
        }).Build(), NullLogger<DealRetentionPruneService>.Instance);

    private async Task<long> CountAsync(string sql)
    {
        await using var c = new NpgsqlConnection(AppCs);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    [TestMethod]
    public async Task RetentionPruner_DeletesOnlyOlderThanCutoff_NeverTheRetainedWindow()
    {
        RequireEnabled();
        var now = new DateTime(2026, 9, 13, 18, 0, 0, DateTimeKind.Utc);
        var cutoff = new DateTime(2025, 9, 13, 0, 0, 0, DateTimeKind.Utc);
        Assert.AreEqual(cutoff, RetentionPolicy.CutoffUtc(now, 12));

        var rows = new List<DealRecord>
        {
            StoreDeal(9001, 10m, cutoff.AddSeconds(-1)),              // just outside -> delete
            StoreDeal(9002, 10m, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)), // far outside -> delete
            StoreDeal(9006, 500m, cutoff.AddDays(-1), action: 2),     // old cash movement: the rule covers all deal rows
            StoreDeal(9003, 10m, cutoff),                             // EXACTLY at the boundary -> keep (deal_time >= cutoff)
            StoreDeal(9004, 10m, cutoff.AddSeconds(1)),               // just inside -> keep
            StoreDeal(9005, 10m, now.AddHours(-1)),                   // recent -> keep
        };
        // 250 more old rows so the batched delete has to loop (batch size 100 -> 3 batches).
        rows.AddRange(Enumerable.Range(0, 250).Select(i => StoreDeal(20_000 + i, 1m, cutoff.AddDays(-2).AddMinutes(-i))));
        await _svc.UpsertDealsAsync(rows);

        // Other history tables are NOT covered by the retention decision and must be untouched.
        await using (var c = new NpgsqlConnection(AppCs))
        {
            await c.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
INSERT INTO trade_audit_log (source, deal_id, login, symbol, field_changed, detected_at) VALUES ('bbook', 9002, 5001, 'XAUUSD', 'profit', '2024-01-01T00:00:00Z');
INSERT INTO bridge_executions (client_deal_id, cen_ord_id, symbol, side, client_volume, client_price, client_time, client_mt_deal_id)
VALUES ('OLD-1', 'ORD-OLD', 'XAUUSD', 'BUY', 1, 2400, '2024-01-01T00:00:00Z', 1);", c);
            await cmd.ExecuteNonQueryAsync();
        }

        var run = await Pruner(12).RunNowAsync("manual", now);

        Assert.IsNull(run.Error, run.Error);
        Assert.AreEqual(cutoff, run.CutoffUtc.ToUniversalTime());
        Assert.AreEqual(253, run.Deleted, "9001 + 9002 + 9006 + the 250 old rows");
        Assert.IsTrue(run.Batches >= 3, $"batched delete looped ({run.Batches} batches)");

        var left = (await _svc.GetDealsAsync("bbook", new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), now.AddDays(1)))
            .Select(d => d.DealId).OrderBy(x => x).ToArray();
        CollectionAssert.AreEqual(new long[] { 9003, 9004, 9005 }, left, "the whole retained window survives, boundary included");
        Assert.AreEqual(3, await CountAsync($"SELECT count(*) FROM deals WHERE deal_time >= '{cutoff:O}'"), "nothing retained was deleted");
        Assert.AreEqual(1, await CountAsync("SELECT count(*) FROM trade_audit_log"), "trade_audit_log untouched");
        Assert.AreEqual(1, await CountAsync("SELECT count(*) FROM bridge_executions"), "bridge_executions untouched");

        var runs = await _svc.ListRetentionPruneRunsAsync(5);
        Assert.IsTrue(runs.Any(r => r.Deleted == 253 && r.Error == null), "the run is recorded");

        var again = await Pruner(12).RunNowAsync("manual", now);
        Assert.AreEqual(0, again.Deleted, "idempotent: a second run on the same day deletes nothing");
    }

    [TestMethod]
    public async Task RetentionPruner_ConfiguredBelowTwelveMonths_IsRefused_NothingDeleted()
    {
        RequireEnabled();
        var now = new DateTime(2026, 9, 13, 18, 0, 0, DateTimeKind.Utc);
        await _svc.UpsertDealsAsync(new[]
        {
            StoreDeal(9101, 10m, now.AddMonths(-7)),   // inside 12 months but outside a mistaken 6
            StoreDeal(9102, 10m, now.AddMonths(-13)),  // outside even 12: still NOT deleted on a refused run
        });

        var run = await Pruner(6).RunNowAsync("scheduled", now);

        StringAssert.StartsWith(run.Error, "refused");
        Assert.AreEqual(0, run.Deleted);
        Assert.AreEqual(2, await CountAsync("SELECT count(*) FROM deals"), "a refused run deletes nothing at all");
        Assert.IsTrue((await _svc.ListRetentionPruneRunsAsync(5)).Any(r => r.Error != null && r.Error.StartsWith("refused")),
            "the refusal is recorded");
    }

    [TestMethod]
    public async Task RetentionCutoff_EqualsTheDocumentedSql()
    {
        RequireEnabled();
        var samples = new[]
        {
            new DateTime(2026, 9, 13, 18, 0, 0, DateTimeKind.Utc),
            new DateTime(2027, 3, 31, 23, 59, 59, DateTimeKind.Utc),
            new DateTime(2028, 2, 29, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        await using var c = new NpgsqlConnection(AppCs);
        await c.OpenAsync();
        // Non-UTC session zones are the point: subtracting months from a timestamptz happens in the
        // session zone, which is exactly how the first version of this SQL went wrong.
        foreach (var zone in new[] { "UTC", "America/Los_Angeles", "Asia/Beirut" })
        {
            await using (var set = new NpgsqlCommand($"SET TimeZone = '{zone}'", c)) await set.ExecuteNonQueryAsync();
            foreach (var now in samples)
            {
                // The expression from db/README.md / import.ps1 with now() replaced by a parameter.
                await using var cmd = new NpgsqlCommand(
                    "SELECT (date_trunc('day', (@now AT TIME ZONE 'UTC')) - interval '12 months') AT TIME ZONE 'UTC'", c);
                cmd.Parameters.AddWithValue("now", now);
                var sql = ((DateTime)(await cmd.ExecuteScalarAsync())!).ToUniversalTime();
                Assert.AreEqual(RetentionPolicy.CutoffUtc(now, 12), sql, $"cutoff for {now:O} in a {zone} session");
            }
        }
    }

    [TestMethod]
    public async Task HistoryReader_ServesTheRangeFromPostgres_OverlaysTheUnpersistedTail_ClampsAtRetention()
    {
        RequireEnabled();
        var now = new DateTime(2026, 9, 13, 18, 0, 0, DateTimeKind.Utc);
        var floor = RetentionPolicy.CutoffUtc(now, 12);

        await _svc.UpsertDealsAsync(new[]
        {
            StoreDeal(9201, 100m, now.AddMonths(-11)),   // old but retained: only Postgres has it (FeedBook is 48 h)
            StoreDeal(9202, 10m, now.AddDays(-2)),       // persisted with an older value
            StoreDeal(9203, 10m, floor.AddDays(-30)),    // older than retention: must not be served
        });

        var workingSet = new DealStore();
        workingSet.AddDeal(FeedDeal(9202, 20m, now.AddDays(-2)));   // newer value of a persisted deal
        workingSet.AddDeal(FeedDeal(9204, 5m, now.AddMinutes(-1))); // delivered, not yet persisted

        var reader = new DealHistoryReader(_svc, workingSet, new ConfigurationBuilder().Build(), () => now);
        var h = await reader.GetClosedDealsAsync(now.AddMonths(-13), now.AddDays(1));

        Assert.IsTrue(h.ClampedByRetention, "a request older than the retained window is clamped and says so");
        Assert.AreEqual(floor, h.EffectiveFromUtc);
        CollectionAssert.AreEqual(new ulong[] { 9201, 9202, 9204 }, h.Deals.Select(d => d.DealId).ToArray());
        Assert.AreEqual(20m, h.Deals.Single(d => d.DealId == 9202).Profit, "the in-memory value wins over the stored row");
        Assert.AreEqual(1, h.FromWorkingSetOnly);
        Assert.AreEqual(1, h.WorkingSetOverrides);

        var bySymbol = DealPnLAggregator.BySymbol(h.Deals);
        Assert.AreEqual(125m, bySymbol.Single().TotalProfit, "100 + 20 + 5 -- the 11-month-old deal is in the P&L");
    }

    [TestMethod]
    public async Task EquityClientConfig_DateColumns_RoundTrip()
    {
        RequireEnabled();
        var cfg = new EquityPnLClientConfig { Login=5001, Source="bbook", CommRebatePct=50m, PsPct=10m,
            PsContractStart=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), PsCumPl=-123.45m, PsLowWaterMark=-200m };
        Assert.IsTrue(await _svc.UpsertEquityPnLClientConfigAsync(cfg));
        var back = (await _svc.GetEquityPnLClientConfigsAsync("bbook")).Single(x => x.Login==5001);
        Assert.AreEqual(50m, back.CommRebatePct);
        Assert.AreEqual(new DateTime(2026,1,1), back.PsContractStart!.Value.Date, "date column preserved (no tz shift)");
        Assert.AreEqual(-123.45m, back.PsCumPl);
    }
}
