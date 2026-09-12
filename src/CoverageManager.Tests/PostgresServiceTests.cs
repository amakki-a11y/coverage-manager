using CoverageManager.Api.Services;
using CoverageManager.Core.Models;
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
            "TRUNCATE deals, symbol_mappings, trading_accounts, exposure_snapshots, equity_pnl_client_config RESTART IDENTITY CASCADE", c);
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
