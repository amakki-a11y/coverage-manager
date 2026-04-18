using Microsoft.AspNetCore.Mvc;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;
using CoverageManager.Connector;
using CoverageManager.Api.Services;

namespace CoverageManager.Api.Controllers;

[ApiController]
[Route("api/exposure")]
public class ExposureController : ControllerBase
{
    private readonly ExposureEngine _exposureEngine;
    private readonly PositionManager _positionManager;
    private readonly MT5ManagerConnection _mt5Connection;
    private readonly DealStore _dealStore;
    private readonly SupabaseService _supabase;
    private readonly ExposureSnapshotService _snapshotService;
    private readonly IHttpClientFactory _httpFactory;

    public ExposureController(
        ExposureEngine exposureEngine,
        PositionManager positionManager,
        MT5ManagerConnection mt5Connection,
        DealStore dealStore,
        SupabaseService supabase,
        ExposureSnapshotService snapshotService,
        IHttpClientFactory httpFactory)
    {
        _exposureEngine = exposureEngine;
        _positionManager = positionManager;
        _mt5Connection = mt5Connection;
        _dealStore = dealStore;
        _supabase = supabase;
        _snapshotService = snapshotService;
        _httpFactory = httpFactory;
    }

    /// <summary>
    /// GET /api/exposure/summary — current exposure snapshot
    /// </summary>
    [HttpGet("summary")]
    public IActionResult GetSummary()
    {
        var exposure = _exposureEngine.CalculateExposure();
        return Ok(exposure);
    }

    /// <summary>
    /// GET /api/exposure/positions — all open positions
    /// </summary>
    [HttpGet("positions")]
    public IActionResult GetPositions()
    {
        var positions = _positionManager.GetAllPositions();
        return Ok(positions);
    }

    /// <summary>
    /// GET /api/exposure/status — MT5 connection status
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            mt5Connected = _mt5Connection.IsConnected,
            mt5Server = _mt5Connection.ConnectedServer,
            bbookPositions = _mt5Connection.PositionCount,
            loginCount = _mt5Connection.LoginCount
        });
    }

    /// <summary>
    /// GET /api/exposure/pnl?from=2026-03-29&to=2026-04-01 — realized P&L with date filtering.
    /// Queries Supabase directly for the date range (persistent history).
    /// Falls back to in-memory DealStore if no dates specified.
    /// </summary>
    [HttpGet("pnl")]
    public async Task<IActionResult> GetPnL([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var fromDate = (from ?? DateTime.UtcNow.Date).Date;
        var toDate = (to ?? DateTime.UtcNow.Date).Date;

        // Interpret the date picker in Lebanon local time so the window matches what
        // the dealer sees in MT5 (also Asia/Beirut) and the Net P&L tab. Without this
        // conversion the two tabs returned different sums for the same date — UTC
        // days are offset ~3h from Lebanon days under DST.
        TimeZoneInfo beirut;
        try { beirut = TimeZoneInfo.FindSystemTimeZoneById("Asia/Beirut"); }
        catch { beirut = TimeZoneInfo.Utc; }
        static DateTime LocalMidnightToUtc(DateTime localDate, TimeZoneInfo tz)
        {
            var local = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
            try { return TimeZoneInfo.ConvertTimeToUtc(local, tz); }
            catch (ArgumentException) { return TimeZoneInfo.ConvertTimeToUtc(local.AddHours(1), tz); }
        }
        var fromUtc = LocalMidnightToUtc(fromDate,               beirut);
        var toUtc   = LocalMidnightToUtc(toDate.AddDays(1),       beirut);

        // Server-side SQL aggregation — replaces a paginated 121K-deal fetch + LINQ
        // GroupBy that took 80+s for 3-week ranges. Sub-second now.
        var movedLogins = await _supabase.GetMovedLoginsAsync();
        var symbols = await _supabase.AggregateBBookPnLFullAsync(
            fromUtc, toUtc, movedLogins.Select(l => (long)l));

        return Ok(new
        {
            totalDeals = symbols.Sum(s => s.DealCount),
            symbols = symbols.OrderByDescending(p => Math.Abs(p.NetPnL)).ToList(),
            from = fromDate,
            to = to ?? DateTime.UtcNow.Date
        });
    }

    /// <summary>
    /// GET /api/exposure/deals — all closed deals
    /// </summary>
    [HttpGet("deals")]
    public IActionResult GetDeals()
    {
        var deals = _dealStore.GetAllDeals();
        return Ok(deals);
    }

    /// <summary>
    /// GET /api/exposure/report?from=2026-03-29&to=2026-04-01 — Manager-style summary report.
    /// Aggregates deals by symbol, login, and day from Supabase.
    /// </summary>
    [HttpGet("report")]
    public async Task<IActionResult> GetReport([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var fromDate = from ?? DateTime.UtcNow.Date;
        var toDate = (to ?? DateTime.UtcNow.Date).AddDays(1);

        var deals = await _supabase.GetDealsAsync("bbook", fromDate, toDate);
        var tradeDeals = deals
            .Where(d => d.Action <= 1 && !string.IsNullOrEmpty(d.Symbol))
            .ToList();

        // Summary totals
        var outDeals = tradeDeals.Where(d => d.Entry == 1 || d.Entry == 2 || d.Entry == 3).ToList();
        var totalProfit = outDeals.Sum(d => d.Profit);
        var totalCommission = tradeDeals.Sum(d => d.Commission);
        var totalSwap = outDeals.Sum(d => d.Swap);
        var totalFee = tradeDeals.Sum(d => d.Fee);
        var totalBuyVolume = tradeDeals.Where(d => d.Direction == "BUY").Sum(d => d.Volume);
        var totalSellVolume = tradeDeals.Where(d => d.Direction == "SELL").Sum(d => d.Volume);

        // By symbol
        var bySymbol = tradeDeals.GroupBy(d => d.Symbol).Select(g =>
        {
            var gOut = g.Where(d => d.Entry == 1 || d.Entry == 2 || d.Entry == 3).ToList();
            return new
            {
                symbol = g.Key,
                deals = g.Count(),
                buyVolume = g.Where(d => d.Direction == "BUY").Sum(d => d.Volume),
                sellVolume = g.Where(d => d.Direction == "SELL").Sum(d => d.Volume),
                totalVolume = g.Sum(d => d.Volume),
                profit = gOut.Sum(d => d.Profit),
                commission = g.Sum(d => d.Commission),
                swap = gOut.Sum(d => d.Swap),
                fee = g.Sum(d => d.Fee),
                netPnL = gOut.Sum(d => d.Profit) + g.Sum(d => d.Commission) + gOut.Sum(d => d.Swap) + g.Sum(d => d.Fee)
            };
        }).OrderByDescending(s => Math.Abs(s.netPnL)).ToList();

        // By login (top traders)
        var byLogin = tradeDeals.GroupBy(d => d.Login).Select(g =>
        {
            var gOut = g.Where(d => d.Entry == 1 || d.Entry == 2 || d.Entry == 3).ToList();
            return new
            {
                login = g.Key,
                deals = g.Count(),
                buyVolume = g.Where(d => d.Direction == "BUY").Sum(d => d.Volume),
                sellVolume = g.Where(d => d.Direction == "SELL").Sum(d => d.Volume),
                profit = gOut.Sum(d => d.Profit),
                commission = g.Sum(d => d.Commission),
                swap = gOut.Sum(d => d.Swap),
                fee = g.Sum(d => d.Fee),
                netPnL = gOut.Sum(d => d.Profit) + g.Sum(d => d.Commission) + gOut.Sum(d => d.Swap) + g.Sum(d => d.Fee),
                symbols = g.Select(d => d.Symbol).Distinct().Count()
            };
        }).OrderByDescending(l => Math.Abs(l.netPnL)).ToList();

        // By day
        var byDay = tradeDeals.GroupBy(d => d.DealTime.Date).OrderBy(g => g.Key).Select(g =>
        {
            var gOut = g.Where(d => d.Entry == 1 || d.Entry == 2 || d.Entry == 3).ToList();
            return new
            {
                date = g.Key.ToString("yyyy-MM-dd"),
                deals = g.Count(),
                buyVolume = g.Where(d => d.Direction == "BUY").Sum(d => d.Volume),
                sellVolume = g.Where(d => d.Direction == "SELL").Sum(d => d.Volume),
                profit = gOut.Sum(d => d.Profit),
                netPnL = gOut.Sum(d => d.Profit) + g.Sum(d => d.Commission) + gOut.Sum(d => d.Swap) + g.Sum(d => d.Fee)
            };
        }).ToList();

        return Ok(new
        {
            from = fromDate,
            to = to ?? DateTime.UtcNow.Date,
            totalDeals = tradeDeals.Count,
            uniqueLogins = tradeDeals.Select(d => d.Login).Distinct().Count(),
            uniqueSymbols = tradeDeals.Select(d => d.Symbol).Distinct().Count(),
            summary = new
            {
                buyVolume = totalBuyVolume,
                sellVolume = totalSellVolume,
                totalVolume = totalBuyVolume + totalSellVolume,
                profit = totalProfit,
                commission = totalCommission,
                swap = totalSwap,
                fee = totalFee,
                netPnL = totalProfit + totalCommission + totalSwap + totalFee
            },
            bySymbol,
            byLogin,
            byDay
        });
    }

    /// <summary>
    /// POST /api/exposure/pnl/reload?from=2026-03-01&to=2026-03-31 — reload deals for date range
    /// </summary>
    [HttpPost("pnl/reload")]
    public IActionResult ReloadPnL([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        if (!_mt5Connection.IsConnected)
            return StatusCode(503, "MT5 not connected");

        var fromOffset = new DateTimeOffset(from.Date, TimeSpan.Zero);
        var toOffset = new DateTimeOffset(to.Date.AddDays(1), TimeSpan.Zero); // Include full end day

        var count = _mt5Connection.ReloadDeals(fromOffset, toOffset);
        if (count < 0)
            return StatusCode(503, "MT5 not ready");

        var pnl = _dealStore.GetPnLBySymbol();
        var daily = _dealStore.GetPnLByDay();
        return Ok(new
        {
            totalDeals = _dealStore.DealCount,
            symbols = pnl,
            daily
        });
    }

    /// <summary>
    /// GET /api/exposure/verify?from=&to= — Compare MT5 Manager deals vs Supabase.
    /// Batches logins (1000/batch) to avoid overloading MT5. Manual trigger only.
    /// </summary>
    [HttpGet("verify")]
    public async Task<IActionResult> VerifyDeals(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] bool fix = false,
        [FromQuery] bool delete = false)
    {
        if (!_mt5Connection.IsConnected)
            return StatusCode(503, new { error = "MT5 not connected" });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var fromDate = from ?? DateTime.UtcNow.Date;
        var toDate = (to ?? DateTime.UtcNow.Date).AddDays(1);
        var fromOffset = new DateTimeOffset(fromDate, TimeSpan.Zero);
        var toOffset = new DateTimeOffset(toDate, TimeSpan.Zero);

        // 1. Query MT5 Manager (batched, read-only)
        var mt5Deals = _mt5Connection.QueryDeals(fromOffset, toOffset);

        // 2. Query Supabase (exclude moved accounts)
        var supaDeals = await _supabase.GetDealsAsync("bbook", fromDate, toDate);
        var movedLogins = await _supabase.GetMovedLoginsAsync();
        var supaTradeDeals = supaDeals
            .Where(d => d.Action <= 1 && !string.IsNullOrEmpty(d.Symbol))
            .Where(d => !movedLogins.Contains(d.Login))
            .ToList();

        // Fix: find deals in MT5 but not in Supabase, upsert them.
        var fixedCount = 0;
        if (fix)
        {
            var supaDealIds = new HashSet<long>(supaTradeDeals.Select(d => d.DealId));
            var missingDeals = mt5Deals
                .Where(d => !supaDealIds.Contains((long)d.DealId))
                .Select(d => new DealRecord
                {
                    DealId = (long)d.DealId,
                    Source = "bbook",
                    Login = (long)d.Login,
                    Symbol = d.Symbol,
                    CanonicalSymbol = d.Symbol,
                    Direction = d.Direction,
                    Action = d.Direction == "BUY" ? 0 : 1,
                    Entry = (int)d.Entry,
                    Volume = d.VolumeLots,
                    Price = d.Price,
                    Profit = d.Profit,
                    Commission = d.Commission,
                    Swap = d.Swap,
                    Fee = d.Fee,
                    OrderId = d.OrderId == 0 ? null : (long?)d.OrderId,
                    PositionId = d.PositionId == 0 ? null : (long?)d.PositionId,
                    DealTime = d.Time
                })
                .ToList();

            if (missingDeals.Count > 0)
            {
                await _supabase.UpsertDealsAsync(missingDeals);
                fixedCount = missingDeals.Count;
            }
        }

        // Delete: find deals in Supabase but not in MT5 (ghost deals — dealer reversed /
        // compliance removed / reassigned) and remove them so future P&L matches MT5.
        // Opt-in because it's destructive. Moved-account deals are already excluded from
        // supaTradeDeals so they won't be caught here.
        var deletedCount = 0;
        var deletedIds = new List<long>();
        if (delete)
        {
            var mt5DealIds = new HashSet<long>(mt5Deals.Select(d => (long)d.DealId));
            deletedIds = supaTradeDeals
                .Where(d => !mt5DealIds.Contains(d.DealId))
                .Select(d => d.DealId)
                .ToList();
            if (deletedIds.Count > 0)
                deletedCount = await _supabase.DeleteDealsAsync("bbook", deletedIds);
        }

        // 3. Aggregate MT5 by symbol
        var mt5BySymbol = mt5Deals
            .GroupBy(d => d.Symbol)
            .ToDictionary(g => g.Key, g =>
            {
                var outDeals = g.Where(d => d.Entry == 1 || d.Entry == 2 || d.Entry == 3);
                return new
                {
                    buyVolume = g.Where(d => d.Direction == "BUY").Sum(d => d.VolumeLots),
                    sellVolume = g.Where(d => d.Direction == "SELL").Sum(d => d.VolumeLots),
                    pnl = outDeals.Sum(d => d.Profit + d.Commission + d.Swap + d.Fee),
                    dealCount = g.Count()
                };
            });

        // 4. Aggregate Supabase by symbol
        var supaBySymbol = supaTradeDeals
            .GroupBy(d => d.Symbol)
            .ToDictionary(g => g.Key, g =>
            {
                var outDeals = g.Where(d => d.Entry == 1 || d.Entry == 2 || d.Entry == 3);
                return new
                {
                    buyVolume = g.Where(d => d.Direction == "BUY").Sum(d => d.Volume),
                    sellVolume = g.Where(d => d.Direction == "SELL").Sum(d => d.Volume),
                    pnl = outDeals.Sum(d => d.Profit + d.Commission + d.Swap + d.Fee),
                    dealCount = g.Count()
                };
            });

        // 5. Compare
        var allSymbols = mt5BySymbol.Keys.Union(supaBySymbol.Keys).OrderBy(s => s).ToList();
        var symbols = allSymbols.Select(symbol =>
        {
            var mt5 = mt5BySymbol.GetValueOrDefault(symbol);
            var supa = supaBySymbol.GetValueOrDefault(symbol);

            var diffBuy = (mt5?.buyVolume ?? 0) - (supa?.buyVolume ?? 0);
            var diffSell = (mt5?.sellVolume ?? 0) - (supa?.sellVolume ?? 0);
            var diffPnl = (mt5?.pnl ?? 0) - (supa?.pnl ?? 0);
            var diffCount = (mt5?.dealCount ?? 0) - (supa?.dealCount ?? 0);

            return new
            {
                symbol,
                mt5 = mt5 != null ? new { mt5.buyVolume, mt5.sellVolume, mt5.pnl, mt5.dealCount } : null,
                supabase = supa != null ? new { supa.buyVolume, supa.sellVolume, supa.pnl, supa.dealCount } : null,
                diff = new { buyVolume = diffBuy, sellVolume = diffSell, pnl = diffPnl, dealCount = diffCount },
                match = Math.Abs(diffBuy) < 0.01m && Math.Abs(diffSell) < 0.01m && diffCount == 0
            };
        }).ToList();

        sw.Stop();
        var matched = symbols.Count(s => s.match);

        return Ok(new
        {
            from = fromDate.ToString("yyyy-MM-dd"),
            to = (toDate.AddDays(-1)).ToString("yyyy-MM-dd"),
            symbols,
            summary = new
            {
                totalSymbols = symbols.Count,
                matched,
                mismatched = symbols.Count - matched
            },
            loginsProcessed = _mt5Connection.LoginCount,
            mt5TotalDeals = mt5Deals.Count,
            supabaseTotalDeals = supaTradeDeals.Count,
            fixed_ = fixedCount,
            deleted_ = deletedCount,
            deletedDealIds = deletedIds,
            elapsed = sw.Elapsed.ToString(@"hh\:mm\:ss")
        });
    }

    /// <summary>
    /// GET /api/exposure/verify/detail?from=&to= — Per-login deal ID comparison.
    /// Queries one login at a time to avoid MT5 load. Shows exact missing/extra deal IDs.
    /// </summary>
    [HttpGet("verify/detail")]
    public async Task<IActionResult> VerifyDealsDetail(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] bool fix = false)
    {
        if (!_mt5Connection.IsConnected)
            return StatusCode(503, new { error = "MT5 not connected" });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var fromDate = from ?? DateTime.UtcNow.Date;
        var toDate = (to ?? DateTime.UtcNow.Date).AddDays(1);
        var fromOffset = new DateTimeOffset(fromDate, TimeSpan.Zero);
        var toOffset = new DateTimeOffset(toDate, TimeSpan.Zero);

        var movedLogins = await _supabase.GetMovedLoginsAsync();
        var allSupaDeals = await _supabase.GetDealsAsync("bbook", fromDate, toDate);
        var supaTradeDeals = allSupaDeals
            .Where(d => d.Action <= 1 && !string.IsNullOrEmpty(d.Symbol))
            .Where(d => !movedLogins.Contains(d.Login))
            .ToList();

        // Group Supabase deals by login
        var supaByLogin = supaTradeDeals
            .GroupBy(d => d.Login)
            .ToDictionary(g => g.Key, g => g.ToList());

        var logins = _mt5Connection.Logins
            .Where(l => !movedLogins.Contains((long)l))
            .ToArray();

        var loginResults = new List<object>();
        var totalMissing = 0;
        var totalExtra = 0;
        var dealsToFix = new List<DealRecord>();

        foreach (var login in logins)
        {
            var mt5Deals = _mt5Connection.QueryDealsForLogin(login, fromOffset, toOffset);
            var mt5DealIds = new HashSet<ulong>(mt5Deals.Select(d => d.DealId));

            var supaDealList = supaByLogin.GetValueOrDefault((long)login) ?? [];
            var supaDealIds = new HashSet<long>(supaDealList.Select(d => d.DealId));

            var missingFromSupa = mt5Deals.Where(d => !supaDealIds.Contains((long)d.DealId)).ToList();
            var extraInSupa = supaDealList.Where(d => !mt5DealIds.Contains((ulong)d.DealId)).ToList();

            if (missingFromSupa.Count == 0 && extraInSupa.Count == 0)
                continue; // Skip matched logins

            totalMissing += missingFromSupa.Count;
            totalExtra += extraInSupa.Count;

            if (fix && missingFromSupa.Count > 0)
            {
                dealsToFix.AddRange(missingFromSupa.Select(d => new DealRecord
                {
                    DealId = (long)d.DealId,
                    Source = "bbook",
                    Login = (long)d.Login,
                    Symbol = d.Symbol,
                    CanonicalSymbol = d.Symbol,
                    Direction = d.Direction,
                    Action = d.Direction == "BUY" ? 0 : 1,
                    Entry = (int)d.Entry,
                    Volume = d.VolumeLots,
                    Price = d.Price,
                    Profit = d.Profit,
                    Commission = d.Commission,
                    Swap = d.Swap,
                    Fee = d.Fee,
                    OrderId = d.OrderId == 0 ? null : (long?)d.OrderId,
                    PositionId = d.PositionId == 0 ? null : (long?)d.PositionId,
                    DealTime = d.Time
                }));
            }

            loginResults.Add(new
            {
                login,
                mt5Count = mt5Deals.Count,
                supaCount = supaDealList.Count,
                missingFromSupabase = missingFromSupa.Count,
                extraInSupabase = extraInSupa.Count,
                missingDeals = missingFromSupa.Take(20).Select(d => new
                {
                    dealId = d.DealId,
                    symbol = d.Symbol,
                    direction = d.Direction,
                    volume = d.VolumeLots,
                    time = d.Time.ToString("yyyy-MM-dd HH:mm:ss")
                }),
                extraDeals = extraInSupa.Take(20).Select(d => new
                {
                    dealId = d.DealId,
                    symbol = d.Symbol,
                    direction = d.Direction,
                    volume = d.Volume,
                    time = d.DealTime.ToString("yyyy-MM-dd HH:mm:ss")
                })
            });
        }

        // Also check Supabase logins not in MT5
        var mt5LoginSet = new HashSet<ulong>(logins);
        var supaOnlyLogins = supaByLogin.Keys
            .Where(l => !mt5LoginSet.Contains((ulong)l))
            .ToList();

        foreach (var login in supaOnlyLogins)
        {
            var deals = supaByLogin[login];
            totalExtra += deals.Count;
            loginResults.Add(new
            {
                login,
                mt5Count = 0,
                supaCount = deals.Count,
                missingFromSupabase = 0,
                extraInSupabase = deals.Count,
                missingDeals = Array.Empty<object>(),
                extraDeals = deals.Take(20).Select(d => new
                {
                    dealId = d.DealId,
                    symbol = d.Symbol,
                    direction = d.Direction,
                    volume = d.Volume,
                    time = d.DealTime.ToString("yyyy-MM-dd HH:mm:ss")
                }),
                note = "Login not in MT5 active list"
            });
        }

        var fixedCount = 0;
        if (fix && dealsToFix.Count > 0)
        {
            await _supabase.UpsertDealsAsync(dealsToFix);
            fixedCount = dealsToFix.Count;
        }

        sw.Stop();

        return Ok(new
        {
            from = fromDate.ToString("yyyy-MM-dd"),
            to = (toDate.AddDays(-1)).ToString("yyyy-MM-dd"),
            loginsChecked = logins.Length,
            loginsWithDiffs = loginResults.Count,
            totalMissingFromSupabase = totalMissing,
            totalExtraInSupabase = totalExtra,
            fixed_ = fixedCount,
            elapsed = sw.Elapsed.ToString(@"hh\:mm\:ss"),
            logins = loginResults
        });
    }

    // =========================================================================
    // Period P&L (new "Net P&L" tab)
    //
    //   FloatingΔ = CurrentFloating − BeginFloating
    //   Settled   = sum of closed-deal P&L within the period
    //   Net       = FloatingΔ + Settled
    //
    // Begin anchor is "end-of-day Lebanon time for (fromDate - 1)":
    // midnight Asia/Beirut of fromDate → UTC (21:00 DST, 22:00 standard).
    // =========================================================================

    /// <summary>GET /api/exposure/pnl/period?from=&amp;to=</summary>
    [HttpGet("pnl/period")]
    public async Task<IActionResult> GetPeriodPnL(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var fromDate = (from ?? DateTime.UtcNow.Date).Date;
        var toDate = (to ?? DateTime.UtcNow.Date).Date;

        // Interpret the date picker in Lebanon local time so the range matches what the
        // user sees on their MT5 terminal (also Asia/Beirut). Convert to UTC only for
        // the actual Supabase/collector queries.
        TimeZoneInfo beirut;
        try { beirut = TimeZoneInfo.FindSystemTimeZoneById("Asia/Beirut"); }
        catch { beirut = TimeZoneInfo.Utc; }

        static DateTime LocalMidnightToUtc(DateTime localDate, TimeZoneInfo tz)
        {
            var local = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
            try { return TimeZoneInfo.ConvertTimeToUtc(local, tz); }
            catch (ArgumentException) { return TimeZoneInfo.ConvertTimeToUtc(local.AddHours(1), tz); }
        }

        // Supabase settled range = [fromDate 00:00 Lebanon, toDate+1d 00:00 Lebanon) in UTC.
        var supabaseFrom = LocalMidnightToUtc(fromDate,            beirut);
        var supabaseTo   = LocalMidnightToUtc(toDate.AddDays(1),   beirut);

        // Begin anchor = fromDate 00:00 in Asia/Beirut → UTC (same function).
        var beginAnchorUtc = supabaseFrom;

        // 2. Begin snapshots (per-symbol, latest ≤ anchor). Split by source —
        //    the row carries both bbook_pnl and coverage_pnl.
        var nearest = await _supabase.GetNearestSnapshotsBeforeAsync(beginAnchorUtc);

        // Normalize every canonical symbol coming from mappings/live/snapshots/deals to a
        // single key so we don't end up with two rows for "GCM6" vs "GCM6-" or "Ut100-" vs "UT100".
        // Strip trailing '-', '.c', '.C' and upper-case. Mapping data has inconsistent case.
        static string NormalizeKey(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var s = raw.Trim();
            // Drop a trailing '.X' suffix like '.c' / '.C' / '.m'
            var dot = s.LastIndexOf('.');
            if (dot >= 0 && s.Length - dot <= 3) s = s.Substring(0, dot);
            // Drop trailing dashes
            while (s.EndsWith("-")) s = s[..^1];
            return s.ToUpperInvariant();
        }

        // 3. Current floating from live positions.
        var liveSummaries = _exposureEngine.CalculateExposure();

        // 4. Settled B-Book via fast SQL aggregation (single RPC call — was 46s for 18d ranges).
        var movedLoginsTask = _supabase.GetMovedLoginsAsync();

        // Coverage settled: fetch from Python collector. Keep best-effort (null on failure).
        Dictionary<string, decimal> covSettledByCanonical = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            var http = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(20);
            var url = $"http://localhost:8100/deals?from={fromDate:yyyy-MM-dd}&to={toDate:yyyy-MM-dd}";
            using var res = await http.GetAsync(url, ct);
            if (res.IsSuccessStatusCode)
            {
                using var stream = await res.Content.ReadAsStreamAsync(ct);
                var dto = await System.Text.Json.JsonSerializer.DeserializeAsync<CollectorDealsResponse>(
                    stream, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);
                if (dto?.Symbols != null)
                {
                    // Collector returns coverage-side symbol names (XAUUSD-, US30.c…).
                    // Map each to canonical via symbol_mappings (case-insensitive).
                    var mappings = await _supabase.GetMappingsAsync();
                    var covToCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var m in mappings)
                        if (!string.IsNullOrEmpty(m.CoverageSymbol))
                            covToCanonical[m.CoverageSymbol] = m.CanonicalName;

                    foreach (var s in dto.Symbols)
                    {
                        var canonical = covToCanonical.TryGetValue(s.Symbol ?? string.Empty, out var c)
                            ? c
                            : s.Symbol;
                        var key = NormalizeKey(canonical);
                        if (string.IsNullOrEmpty(key)) continue;
                        covSettledByCanonical[key] = covSettledByCanonical.GetValueOrDefault(key) + s.NetPnL;
                    }
                }
            }
        }
        catch (Exception ex) { /* best-effort */ _ = ex; }

        var movedLogins = await movedLoginsTask;
        // Single RPC returns {canonical_key, net_pnl} per symbol — already normalized server-side.
        var bbookSettledByCanonical = await _supabase.AggregateBBookSettledPnlAsync(
            supabaseFrom, supabaseTo, movedLogins.Select(l => (long)l));

        // Index live summaries by normalized key, and nearest snapshots too.
        var liveByKey = new Dictionary<string, ExposureSummary>(StringComparer.Ordinal);
        foreach (var s in liveSummaries)
        {
            var k = NormalizeKey(s.CanonicalSymbol);
            if (string.IsNullOrEmpty(k)) continue;
            // If two mapping variants produced separate live summaries, merge them.
            if (liveByKey.TryGetValue(k, out var existing))
            {
                existing.BBookPnL += s.BBookPnL;
                existing.CoveragePnL += s.CoveragePnL;
                existing.BBookBuyVolume += s.BBookBuyVolume;
                existing.BBookSellVolume += s.BBookSellVolume;
                existing.CoverageBuyVolume += s.CoverageBuyVolume;
                existing.CoverageSellVolume += s.CoverageSellVolume;
            }
            else
            {
                liveByKey[k] = s;
            }
        }
        // Snapshots. "Sentinel" canonicals (seed anchor rows like BEGIN_SEED_TOTAL or anything
        // prefixed "__") contribute to TOTAL only — not per-symbol rows. They exist so a dealer
        // can seed a portfolio-level Begin without filling in every symbol.
        var snapByKey = new Dictionary<string, ExposureSnapshot>(StringComparer.Ordinal);
        var sentinelSnapshots = new List<ExposureSnapshot>();
        static bool IsSentinelSymbol(string s) =>
            s.StartsWith("__") || s.Contains("SEED_TOTAL", StringComparison.OrdinalIgnoreCase) || s.Contains("PORTFOLIO", StringComparison.OrdinalIgnoreCase);
        foreach (var kvp in nearest)
        {
            var k = NormalizeKey(kvp.Key);
            if (string.IsNullOrEmpty(k)) continue;
            if (IsSentinelSymbol(kvp.Key) || IsSentinelSymbol(k))
            {
                sentinelSnapshots.Add(kvp.Value);
            }
            else
            {
                snapByKey[k] = kvp.Value;
            }
        }

        // 5. Build per-symbol rows. Union of all normalized keys we've seen anywhere.
        var allSymbols = new HashSet<string>(StringComparer.Ordinal);
        foreach (var k in liveByKey.Keys) allSymbols.Add(k);
        foreach (var k in snapByKey.Keys) allSymbols.Add(k);
        foreach (var k in bbookSettledByCanonical.Keys) allSymbols.Add(k);
        foreach (var k in covSettledByCanonical.Keys) allSymbols.Add(k);

        var rows = new List<PeriodPnLRow>();
        foreach (var symUpper in allSymbols.OrderBy(s => s))
        {
            liveByKey.TryGetValue(symUpper, out var live);
            snapByKey.TryGetValue(symUpper, out var snap);

            // A B-Book "open position" exists when the live summary has non-zero volume on that side.
            bool bbHasPos = live != null && (live.BBookBuyVolume != 0 || live.BBookSellVolume != 0);
            bool covHasPos = live != null && (live.CoverageBuyVolume != 0 || live.CoverageSellVolume != 0);

            var bbook = new PeriodPnLSide
            {
                BeginFloating = snap?.BBookPnL ?? 0m,
                CurrentFloating = live?.BBookPnL ?? 0m,
                Settled = bbookSettledByCanonical.GetValueOrDefault(symUpper),
                BeginFromSnapshot = snap != null,
                HasOpenPosition = bbHasPos,
            };
            bbook.FloatingDelta = bbook.CurrentFloating - bbook.BeginFloating;
            bbook.Net = bbook.FloatingDelta + bbook.Settled;

            var coverage = new PeriodPnLSide
            {
                BeginFloating = snap?.CoveragePnL ?? 0m,
                CurrentFloating = live?.CoveragePnL ?? 0m,
                Settled = covSettledByCanonical.GetValueOrDefault(symUpper),
                BeginFromSnapshot = snap != null,
                HasOpenPosition = covHasPos,
            };
            coverage.FloatingDelta = coverage.CurrentFloating - coverage.BeginFloating;
            coverage.Net = coverage.FloatingDelta + coverage.Settled;

            rows.Add(new PeriodPnLRow
            {
                CanonicalSymbol = live?.CanonicalSymbol ?? snap?.CanonicalSymbol ?? symUpper,
                BBook = bbook,
                Coverage = coverage,
                // Broker-perspective edge: positive = broker made money overall.
                // Broker takes the opposite of clients → broker P&L = Coverage − Clients.
                Edge = new PeriodPnLEdge
                {
                    Floating = coverage.FloatingDelta - bbook.FloatingDelta,
                    Settled = coverage.Settled - bbook.Settled,
                    Net = coverage.Net - bbook.Net,
                },
            });
        }

        // 6. Totals — sum every field (missing Begin already counted as 0 per decision #4).
        var totals = new PeriodPnLRow { CanonicalSymbol = "TOTAL" };
        foreach (var r in rows)
        {
            totals.BBook.BeginFloating   += r.BBook.BeginFloating;
            totals.BBook.CurrentFloating += r.BBook.CurrentFloating;
            totals.BBook.FloatingDelta   += r.BBook.FloatingDelta;
            totals.BBook.Settled         += r.BBook.Settled;
            totals.BBook.Net             += r.BBook.Net;
            totals.Coverage.BeginFloating   += r.Coverage.BeginFloating;
            totals.Coverage.CurrentFloating += r.Coverage.CurrentFloating;
            totals.Coverage.FloatingDelta   += r.Coverage.FloatingDelta;
            totals.Coverage.Settled         += r.Coverage.Settled;
            totals.Coverage.Net             += r.Coverage.Net;
        }
        // Sentinel (seed) snapshot contributions: feed portfolio-level Begin into the
        // TOTAL row and propagate through FloatingDelta and Net so the aggregate reflects
        // the dealer's seeded anchor. They don't emit per-symbol rows.
        foreach (var s in sentinelSnapshots)
        {
            totals.BBook.BeginFloating    += s.BBookPnL;
            totals.BBook.FloatingDelta    -= s.BBookPnL;      // delta = current - begin, so subtract begin
            totals.BBook.Net              -= s.BBookPnL;      // net = delta + settled
            totals.Coverage.BeginFloating += s.CoveragePnL;
            totals.Coverage.FloatingDelta -= s.CoveragePnL;
            totals.Coverage.Net           -= s.CoveragePnL;
        }
        totals.Edge.Floating = totals.Coverage.FloatingDelta - totals.BBook.FloatingDelta;
        totals.Edge.Settled = totals.Coverage.Settled - totals.BBook.Settled;
        totals.Edge.Net = totals.Coverage.Net - totals.BBook.Net;
        totals.BBook.BeginFromSnapshot = sentinelSnapshots.Count > 0 || rows.All(r => r.BBook.BeginFromSnapshot);
        totals.Coverage.BeginFromSnapshot = sentinelSnapshots.Count > 0 || rows.All(r => r.Coverage.BeginFromSnapshot);
        totals.BBook.HasOpenPosition = rows.Any(r => r.BBook.HasOpenPosition);
        totals.Coverage.HasOpenPosition = rows.Any(r => r.Coverage.HasOpenPosition);

        return Ok(new PeriodPnLResponse
        {
            From = fromDate,
            To = toDate,
            BeginAnchorUtc = beginAnchorUtc,
            Rows = rows,
            Totals = totals,
        });
    }

    /// <summary>POST /api/exposure/snapshot — trigger an immediate manual snapshot capture.</summary>
    [HttpPost("snapshot")]
    public async Task<IActionResult> CaptureSnapshotNow([FromBody] CaptureSnapshotRequest? body, CancellationToken ct)
    {
        var label = body?.Label ?? string.Empty;
        var count = await _snapshotService.RunNowAsync(triggerType: "manual", label: label, ct);
        return Ok(new { captured = count, at = DateTime.UtcNow });
    }

    /// <summary>GET /api/exposure/snapshots?from=&amp;to=&amp;symbol= — list stored snapshots for diagnostics.</summary>
    [HttpGet("snapshots")]
    public async Task<IActionResult> ListSnapshots(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? symbol = null)
    {
        var fromUtc = from ?? DateTime.UtcNow.AddDays(-30);
        var toUtc = to ?? DateTime.UtcNow;
        var list = await _supabase.ListExposureSnapshotsAsync(fromUtc, toUtc, symbol);
        return Ok(list);
    }

    public sealed class CaptureSnapshotRequest
    {
        public string? Label { get; set; }
    }

    private sealed class CollectorDealsResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("symbols")]
        public List<CollectorSymbolRow>? Symbols { get; set; }
    }
    private sealed class CollectorSymbolRow
    {
        [System.Text.Json.Serialization.JsonPropertyName("symbol")] public string? Symbol { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("netPnL")] public decimal NetPnL { get; set; }
    }
}
