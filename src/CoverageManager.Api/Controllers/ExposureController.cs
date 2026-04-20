using Microsoft.AspNetCore.Mvc;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;
using CoverageManager.Core.Models.EquityPnL;
using CoverageManager.Connector;
using CoverageManager.Api.Services;

namespace CoverageManager.Api.Controllers;

/// <summary>
/// The widest-surface controller in the API. Serves the live Exposure view,
/// historical P&amp;L (both the original settled-only tab and the newer Net P&amp;L
/// period tab), manual backfills, deal verification, snapshot capture, and
/// the Equity P&amp;L endpoint used by the Equity P&amp;L tab.
///
/// <para>Route conventions:
///   * <c>GET  /summary</c>            — live exposure (WebSocket also available)
///   * <c>GET  /positions</c>          — all open positions
///   * <c>GET  /status</c>             — MT5 connection status
///   * <c>GET  /pnl?from&amp;to</c>        — per-symbol settled P&amp;L (legacy P&amp;L tab)
///   * <c>GET  /pnl/period?from&amp;to</c> — floating + settled decomposition (Net P&amp;L tab)
///   * <c>POST /pnl/reload</c>          — re-pull deals from MT5 for a date range
///   * <c>GET  /verify?from&amp;to&amp;fix</c> — compare MT5 ↔ Supabase; fix=true backfills missing
///   * <c>POST /snapshot</c>            — capture an exposure snapshot now
///   * <c>GET  /snapshots</c>           — raw snapshot history
///   * <c>GET  /equity-pnl</c>          — per-login Equity P&amp;L with rebate/PS resolution
///   * <c>POST /equity-pnl/backfill-cash-movements</c> — one-shot MT5 scan for balance/credit deals
///   * <c>GET  /equity-pnl/account-live</c> — diagnostic live MT5 account read
/// </para>
///
/// <para>All date-only query params are interpreted as midnight
/// <c>Asia/Beirut</c> converted to UTC (DST-aware). See CLAUDE.md → "Date +
/// Timezone Model" for why.</para>
/// </summary>
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
    private readonly ILogger<ExposureController> _logger;

    public ExposureController(
        ExposureEngine exposureEngine,
        PositionManager positionManager,
        MT5ManagerConnection mt5Connection,
        DealStore dealStore,
        SupabaseService supabase,
        ExposureSnapshotService snapshotService,
        IHttpClientFactory httpFactory,
        ILogger<ExposureController> logger)
    {
        _exposureEngine = exposureEngine;
        _positionManager = positionManager;
        _mt5Connection = mt5Connection;
        _dealStore = dealStore;
        _supabase = supabase;
        _snapshotService = snapshotService;
        _httpFactory = httpFactory;
        _logger = logger;
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
                    // Preserve MT5's real DealAction code — see DataSyncService
                    // for rationale (balance/credit/correction deals must not
                    // be re-classified to a trade action on upsert).
                    Action = (int)d.Action,
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
                    // Preserve MT5's real DealAction code — see DataSyncService
                    // for rationale (balance/credit/correction deals must not
                    // be re-classified to a trade action on upsert).
                    Action = (int)d.Action,
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

    // =========================================================================
    // GET /api/equity-pnl?from=&to=
    // Phase 1: per-login equity decomposition across the requested Beirut window.
    // On-demand MT5 deal queries → classification → PS HWM advance → totals.
    // =========================================================================
    [HttpGet("/api/equity-pnl")]
    public async Task<IActionResult> GetEquityPnL(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var fromDate = (from ?? DateTime.UtcNow.Date).Date;
        var toDate   = (to   ?? DateTime.UtcNow.Date).Date;

        TimeZoneInfo beirut;
        try { beirut = TimeZoneInfo.FindSystemTimeZoneById("Asia/Beirut"); }
        catch { beirut = TimeZoneInfo.Utc; }

        static DateTime LocalMidnightToUtc(DateTime localDate, TimeZoneInfo tz)
        {
            var local = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
            try { return TimeZoneInfo.ConvertTimeToUtc(local, tz); }
            catch (ArgumentException) { return TimeZoneInfo.ConvertTimeToUtc(local.AddHours(1), tz); }
        }

        var fromUtc = LocalMidnightToUtc(fromDate,            beirut);
        var toUtc   = LocalMidnightToUtc(toDate.AddDays(1),   beirut);

        // ── Fetch the pieces ──────────────────────────────────────────────
        var accountsTask     = _supabase.GetTradingAccountsAsync();
        var configsTask      = _supabase.GetEquityPnLClientConfigsAsync();
        var ratesTask        = _supabase.GetSpreadRebateRatesAsync();
        var movedLoginsTask  = _supabase.GetMovedLoginsAsync();
        var beginSnapsTask   = _supabase.GetAccountEquitySnapshotsBeforeAsync(fromUtc);

        // Phase 2 — per-group overrides. Groups + memberships + group-level
        // rebate/PS config + group spread-rebate rates fetched in parallel.
        var groupConfigsTask     = _supabase.GetGroupConfigsAsync();
        var groupMembersTask     = _supabase.GetLoginGroupMembersAsync(null);
        var groupSpreadRatesTask = _supabase.GetGroupSpreadRebateRatesAsync(null);
        // Non-trade deals (balance / credit / correction) for every bbook
        // login in the window. Small, fast query. Gives us Net Cred + Adj
        // columns — but Net Dep/W is derived separately via balance
        // reconciliation below, because MT5 Manager's admin balance-transfer
        // operations don't surface in RequestDeals (they silently change the
        // account's balance without emitting an action=2 deal row).
        var allDealsTask     = _supabase.GetNonTradeDealsAsync("bbook", fromUtc, toUtc);

        // Per-login trade-deal balance flow (profit + commission + swap + fee)
        // needed for the balance reconciliation that backs out an implicit
        // Net Dep/W. Server-side aggregation RPC is preferable; this
        // pagination-based query is fine for the window sizes we expect
        // (≤ a month, ≤ 200K rows).
        var tradeFlowTask    = _supabase.SumTradeBalanceFlowPerLoginAsync("bbook", fromUtc, toUtc);

        // Coverage (LP) trade flow: the Python collector is the ONLY source of
        // truth for LP deals — they're never synced into Supabase. Without
        // this, the balance-reconciliation below attributes the LP's entire
        // trading loss to Net Dep/W instead of Pl. See CLAUDE.md "No coverage
        // section today" — this path works around that gap per-request.
        var coverageTradeFlowTask = FetchCoverageTradeFlowAsync(fromDate, toDate, ct);

        await Task.WhenAll(accountsTask, configsTask, ratesTask, movedLoginsTask, beginSnapsTask, allDealsTask, tradeFlowTask,
                           groupConfigsTask, groupMembersTask, groupSpreadRatesTask, coverageTradeFlowTask);

        var (coverageTradeFlowLogin, coverageTradeFlowValue) = coverageTradeFlowTask.Result;

        var accounts     = accountsTask.Result;
        var configs      = configsTask.Result.ToDictionary(c => $"{c.Source}:{c.Login}", StringComparer.OrdinalIgnoreCase);
        var ratesByLogin = ratesTask.Result
            .GroupBy(r => $"{r.Source}:{r.Login}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(r => r.CanonicalSymbol.ToUpperInvariant(), r => r.RatePerLot,
                    StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
        var moved       = movedLoginsTask.Result;
        var beginSnaps  = beginSnapsTask.Result;
        var tradeFlow   = tradeFlowTask.Result; // login -> (profit+commission+swap+fee) sum

        // Phase 2 resolution maps. For each (login, source), pre-compute the
        // highest-priority group's config + spread rates so the per-login loop
        // is a constant-time dictionary lookup.
        var groupConfigsById = groupConfigsTask.Result
            .ToDictionary(c => c.GroupId);
        var groupSpreadById = groupSpreadRatesTask.Result
            .GroupBy(r => r.GroupId)
            .ToDictionary(g => g.Key,
                g => g.ToDictionary(r => r.CanonicalSymbol.ToUpperInvariant(), r => r.RatePerLot,
                    StringComparer.OrdinalIgnoreCase));
        // Map each member login -> the group it inherits from. If a login is in
        // multiple groups, the highest `priority` wins; ties broken by group_id
        // (stable). SQL already orders by priority desc, so `First` wins.
        var groupForLogin = groupMembersTask.Result
            .GroupBy(m => $"{m.Source}:{m.Login}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key,
                g => g.OrderByDescending(m => m.Priority).First().GroupId);

        // Trade deals for rebate-eligible logins only. A login is eligible if
        // it has a direct config row, a direct spread-rate row, OR inherits
        // from a group that has config / spread rates. Fetching trade deals
        // only for these logins keeps the payload bounded (avoids the 120k-row
        // full-scan we saw in Phase 1).
        var eligibleLogins = new HashSet<long>();
        foreach (var cfg in configsTask.Result) eligibleLogins.Add(cfg.Login);
        foreach (var r   in ratesTask.Result)   eligibleLogins.Add(r.Login);
        foreach (var m in groupMembersTask.Result)
        {
            var gid = m.GroupId;
            if (groupConfigsById.ContainsKey(gid) || groupSpreadById.ContainsKey(gid))
                eligibleLogins.Add(m.Login);
        }
        var tradeDeals = eligibleLogins.Count > 0
            ? await _supabase.GetTradeDealsForLoginsAsync("bbook", eligibleLogins, fromUtc, toUtc)
            : new List<DealRecord>();
        var tradeDealsByLogin = tradeDeals
            .GroupBy(d => d.Login)
            .ToDictionary(
                g => g.Key,
                g => g.Select(d => new ClosedDeal
                {
                    DealId = (ulong)d.DealId,
                    Login  = (ulong)d.Login,
                    Symbol = d.Symbol ?? string.Empty,
                    Direction = d.Direction ?? string.Empty,
                    VolumeLots = d.Volume,
                    Price = d.Price,
                    Profit = d.Profit,
                    Commission = d.Commission,
                    Swap = d.Swap,
                    Fee = d.Fee,
                    Entry = (uint)d.Entry,
                    Action = (uint)d.Action,
                    Time = d.DealTime,
                }).ToList());

        // Deals grouped by login and projected to the ClosedDeal shape the
        // engine expects (DealRecord.Action maps 1:1 to ClosedDeal.Action).
        var supaDealsByLogin = allDealsTask.Result
            .GroupBy(d => d.Login)
            .ToDictionary(
                g => g.Key,
                g => g.Select(d => new ClosedDeal
                {
                    DealId = (ulong)d.DealId,
                    Login = (ulong)d.Login,
                    Symbol = d.Symbol ?? string.Empty,
                    Direction = d.Direction ?? string.Empty,
                    VolumeLots = d.Volume,
                    Price = d.Price,
                    Profit = d.Profit,
                    Commission = d.Commission,
                    Swap = d.Swap,
                    Fee = d.Fee,
                    Entry = (uint)d.Entry,
                    Action = (uint)d.Action,
                    OrderId = (ulong)(d.OrderId ?? 0),
                    PositionId = (ulong)(d.PositionId ?? 0),
                    Time = d.DealTime,
                }).ToList());

        // ── Normalize a raw symbol to canonical (matches engine's needs) ──
        static string NormalizeKey(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var s = raw.Trim();
            var dot = s.LastIndexOf('.');
            if (dot >= 0 && s.Length - dot <= 3) s = s.Substring(0, dot);
            while (s.EndsWith("-")) s = s[..^1];
            return s.ToUpperInvariant();
        }

        var clientRows   = new List<EquityPnLRow>();
        var coverageRows = new List<EquityPnLRow>();
        var staleConfigs = new List<EquityPnLClientConfig>(); // updated HWM state to flush

        // ── Build rows one account at a time ───────────────────────────────
        foreach (var acct in accounts)
        {
            if (acct.Status != "active") continue;
            if (moved.Contains(acct.Login))  continue;

            var key = $"{acct.Source}:{acct.Login}";

            var beginSnap = beginSnaps.TryGetValue(key, out var bs) ? bs : null;
            decimal? beginEquity = beginSnap?.Equity;

            // Current Equity = live from trading_accounts. Snapshots are the
            // Begin source only — treating the nearest-past snapshot as
            // "Current for a historical range" collapses the PL column to zero
            // whenever a dealer picks a range older than today with no
            // granular daily snapshots yet. Live is more useful until we have
            // dense snapshot coverage.
            var currentEquity = acct.Equity;
            var currentIsLive = true;

            EquityPnLClientConfig? cfg = configs.TryGetValue(key, out var c) ? c : null;

            // Phase 2 inheritance. Resolve the effective login config:
            //   1. If a login-specific row exists, use it verbatim.
            //   2. Else if the login belongs to a group with a config row,
            //      synthesize a config from the group (rebate/PS rates),
            //      keeping zero HWM state so the PS engine doesn't misfire
            //      on first contact with this login.
            //   3. Else cfg stays null (zero rates, column is 0).
            Guid? inheritedGroupId = null;
            if (cfg == null && groupForLogin.TryGetValue(key, out var gid)
                && groupConfigsById.TryGetValue(gid, out var gcfg))
            {
                inheritedGroupId = gid;
                cfg = new EquityPnLClientConfig
                {
                    Login = acct.Login,
                    Source = acct.Source,
                    CommRebatePct = gcfg.CommRebatePct,
                    PsPct = gcfg.PsPct,
                    PsContractStart = null,        // PS at group level doesn't auto-start without login opt-in
                    PsCumPl = 0m,
                    PsLowWaterMark = 0m,
                    PsLastProcessedMonth = null,
                };
            }

            // Effective spread-rebate map = login-level rates OR the group's
            // rates (when login belongs to a group). Login-level rates override
            // group rates for the same symbol.
            var rateMap = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            if (groupForLogin.TryGetValue(key, out var grSpreadId)
                && groupSpreadById.TryGetValue(grSpreadId, out var grRates))
            {
                foreach (var kv in grRates) rateMap[kv.Key] = kv.Value;
            }
            if (ratesByLogin.TryGetValue(key, out var rm))
            {
                foreach (var kv in rm) rateMap[kv.Key] = kv.Value; // login-level wins
            }

            // Populate deal classification columns (Net Dep/W, Net Cred, Adj,
            // plus comm/spread rebates when a config or group membership
            // exists). Non-trade deals come from `supaDealsByLogin` (action >= 2,
            // fetched once for all bbook logins). Trade deals come from
            // `tradeDealsByLogin` (action < 2, fetched only for rebate-eligible
            // logins — avoids a 120k-row full-scan every request).
            List<ClosedDeal> deals = new();
            if (acct.Source == "bbook")
            {
                if (supaDealsByLogin.TryGetValue(acct.Login, out var nonTrade))
                    deals.AddRange(nonTrade);
                if (tradeDealsByLogin.TryGetValue(acct.Login, out var trade))
                    deals.AddRange(trade);
            }

            // Monthly P&L series for PS HWM: built from month-end snapshots only
            // when a PS contract exists. Cheap no-op when disabled.
            List<(DateTime MonthEndUtc, decimal MonthlyPl)>? monthlyPl = null;
            if (cfg?.PsContractStart != null && cfg.PsPct > 0m)
            {
                monthlyPl = BuildMonthlyPl(acct, cfg, beirut);
            }

            var row = EquityPnLEngine.BuildRow(
                acct,
                beginEquity,
                currentEquity,
                currentIsLive,
                deals,
                cfg,
                rateMap,
                NormalizeKey,
                monthlyPl,
                fromUtc,
                toUtc,
                out var updatedCfg);

            // Balance-and-credit reconciliation override for Net Dep/W and
            // Net Credit. MT5 Manager's `RequestDeals` doesn't surface
            // admin balance/credit transfers — they silently shift value
            // between the Balance and Credit buckets with no deal record.
            // The authoritative figures match the Summary Report:
            //   NetDepW = (CurrBalance - BeginBalance) - TradeBalanceFlow
            //   NetCred = CurrCredit  - BeginCredit
            // Applied for both bbook AND coverage as soon as we have a begin
            // snapshot. Coverage trade flow comes from the Python collector
            // (LP deals aren't synced to Supabase), bbook from the Supabase
            // aggregation above.
            if (beginSnap != null)
            {
                decimal tradeBalanceFlow;
                if (acct.Source == "coverage" && coverageTradeFlowLogin.HasValue && acct.Login == coverageTradeFlowLogin.Value)
                    tradeBalanceFlow = coverageTradeFlowValue;
                else
                    tradeBalanceFlow = tradeFlow.TryGetValue(acct.Login, out var tf) ? tf : 0m;

                row.NetDepositWithdraw = (acct.Balance - beginSnap.Balance) - tradeBalanceFlow;
                row.NetCredit          = acct.Credit  - beginSnap.Credit;

                // Recompute downstream columns since we changed two inputs.
                row.SupposedEquity = row.BeginEquity + row.NetDepositWithdraw + row.NetCredit;
                row.Pl             = row.CurrentEquity - row.SupposedEquity;
                var nonTrading     = row.CommRebate + row.SpreadRebate + row.Adjustment + row.ProfitShare;
                // Sign convention locked in Phase 1: clients strip rebates/PS
                // out of NetPL (they're broker outlays); coverage keeps them
                // in NetPL (they're broker income when received from LP).
                row.NetPl = acct.Source == "coverage"
                    ? row.Pl + nonTrading
                    : row.Pl - nonTrading;
            }

            (acct.Source == "coverage" ? coverageRows : clientRows).Add(row);
            // Only persist HWM state back to Supabase when the login has its
            // OWN config row. Inherited-from-group configs are synthetic —
            // writing them back would create phantom per-login rows that
            // detach from the group on the next render.
            if (updatedCfg != null && inheritedGroupId == null) staleConfigs.Add(updatedCfg);
        }

        // Flush any PS state that advanced this run.
        if (staleConfigs.Count > 0)
        {
            await _supabase.UpsertEquityPnLClientConfigsAsync(staleConfigs);
        }

        var clientsTotal  = EquityPnLEngine.Total(clientRows,   "bbook");
        var coverageTotal = EquityPnLEngine.Total(coverageRows, "coverage");

        var brokerEdge = -clientsTotal.NetPl + coverageTotal.NetPl;

        var response = new EquityPnLResponse
        {
            From = fromDate.ToString("yyyy-MM-dd"),
            To   = toDate.ToString("yyyy-MM-dd"),
            BeginAnchorUtc = fromUtc,
            EndAnchorUtc   = toUtc,
            ClientRows   = clientRows.OrderBy(r => r.Login).ToList(),
            CoverageRows = coverageRows.OrderBy(r => r.Login).ToList(),
            ClientsTotal  = clientsTotal,
            CoverageTotal = coverageTotal,
            BrokerEdge    = brokerEdge,
        };
        return Ok(response);
    }

    // =========================================================================
    // GET /api/equity-pnl/account-live?login=NNN
    //
    // Diagnostic: return MT5 Manager's live snapshot of one account
    // (balance / credit / equity / margin) bypassing the 5-min sync cycle.
    // Handy for verifying HR sub-accounts that GetUserLogins('*') doesn't
    // return — their row in trading_accounts can go stale since the sync
    // only touches logins the pool surfaces.
    // =========================================================================
    [HttpGet("/api/equity-pnl/account-live")]
    public IActionResult GetAccountLive([FromQuery] ulong login)
    {
        if (login == 0) return BadRequest(new { error = "login required" });
        var raw = _mt5Connection.QueryUserAccount(login);
        if (raw == null) return NotFound(new { error = "MT5 not connected, or login not found" });
        return Ok(new
        {
            login    = raw.Login,
            name     = raw.Name,
            group    = raw.Group,
            balance  = raw.Balance,
            credit   = raw.Credit,
            equity   = raw.Equity,
            margin   = raw.Margin,
            freeMargin = raw.FreeMargin,
            currency = raw.Currency,
        });
    }

    // =========================================================================
    // POST /api/equity-pnl/backfill-cash-movements?from=&to=
    //
    // One-shot historical backfill of balance (action=2), credit (action=3),
    // charge (4), correction (5), bonus (6), and commission-balance (7) deals
    // into Supabase `deals`. Live deal ingestion now persists every action
    // code automatically; this endpoint is for the window that pre-dates that
    // change (or to re-sync if the dealer notices gaps).
    //
    // Paced at ~1 login / 300 ms to keep MT5 Manager's native side stable —
    // simultaneous bulk queries crashed the process in earlier attempts.
    // =========================================================================
    [HttpPost("/api/equity-pnl/backfill-cash-movements")]
    public async Task<IActionResult> BackfillCashMovements(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var fromDate = (from ?? DateTime.UtcNow.Date.AddDays(-30)).Date;
        var toDate   = (to   ?? DateTime.UtcNow.Date).Date;
        var fromUtc  = DateTime.SpecifyKind(fromDate, DateTimeKind.Utc);
        var toUtc    = DateTime.SpecifyKind(toDate.AddDays(1), DateTimeKind.Utc);

        var accounts = await _supabase.GetTradingAccountsAsync("bbook");
        var moved    = await _supabase.GetMovedLoginsAsync();
        accounts     = accounts
            .Where(a => a.Status == "active" && !moved.Contains(a.Login))
            .ToList();

        var totalFetched = 0;
        var totalPersisted = 0;
        var logins = accounts.Count;
        var errors = 0;

        foreach (var acct in accounts)
        {
            if (ct.IsCancellationRequested) break;

            List<ClosedDeal> deals;
            try
            {
                deals = _mt5Connection.QueryAllDealsForLogin((ulong)acct.Login,
                    new DateTimeOffset(fromUtc), new DateTimeOffset(toUtc));
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogWarning(ex, "backfill-cash-movements query failed for {Login}", acct.Login);
                await Task.Delay(300, ct).ConfigureAwait(false);
                continue;
            }

            // Only non-trade deals — trade deals are already persisted by the
            // existing DataSyncService path.
            var nonTrade = deals.Where(d => d.Action >= 2).ToList();
            totalFetched += nonTrade.Count;

            if (nonTrade.Count > 0)
            {
                var records = nonTrade.Select(d => new DealRecord
                {
                    Source = "bbook",
                    DealId = (long)d.DealId,
                    Login = (long)d.Login,
                    Symbol = d.Symbol ?? string.Empty,
                    CanonicalSymbol = d.Symbol ?? string.Empty,
                    Direction = d.Direction ?? string.Empty,
                    Action = (int)d.Action,
                    Entry = (int)d.Entry,
                    Volume = d.VolumeLots,
                    Price = d.Price,
                    Profit = d.Profit,
                    Commission = d.Commission,
                    Swap = d.Swap,
                    Fee = d.Fee,
                    OrderId = d.OrderId == 0 ? null : (long)d.OrderId,
                    PositionId = d.PositionId == 0 ? null : (long)d.PositionId,
                    DealTime = d.Time,
                }).ToList();
                totalPersisted += await _supabase.UpsertDealsAsync(records);
            }

            // Pacing — native side is unhappy under sustained burst load.
            await Task.Delay(300, ct).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "backfill-cash-movements: range {From:yyyy-MM-dd}..{To:yyyy-MM-dd}, {Logins} logins, {Fetched} non-trade deals fetched, {Persisted} persisted, {Errors} login errors",
            fromDate, toDate, logins, totalFetched, totalPersisted, errors);

        return Ok(new
        {
            from = fromDate.ToString("yyyy-MM-dd"),
            to = toDate.ToString("yyyy-MM-dd"),
            logins,
            fetched = totalFetched,
            persisted = totalPersisted,
            errors,
        });
    }

    /// <summary>
    /// Build an ordered list of (month-end UTC, monthly-trading-P&amp;L) pairs for
    /// the PS HWM engine. Covers every month from the contract start through
    /// the current window, allowing the engine to catch up on any missed months.
    /// Monthly P&amp;L = (equity at month-end) - (equity at prev month-end) - (cash in/out that month).
    /// </summary>
    /// <remarks>
    /// For Phase 1, this uses month-end equity snapshots only (no cash-movement subtraction).
    /// That means the HWM engine treats deposits/withdrawals as if they're trading P&amp;L.
    /// Refinement in Phase 2: subtract within-month balance-deal sums. For the demo,
    /// dealer is responsible for pausing PS contracts during significant cash events.
    /// </remarks>
    private List<(DateTime MonthEndUtc, decimal MonthlyPl)> BuildMonthlyPl(
        TradingAccount acct,
        EquityPnLClientConfig cfg,
        TimeZoneInfo beirut)
    {
        var list = new List<(DateTime, decimal)>();
        if (cfg.PsContractStart == null) return list;

        var start = cfg.PsContractStart.Value.Date;
        var today = DateTime.UtcNow.Date;

        // We don't have snapshots before the feature existed, so this walk can't
        // produce values for months that pre-date our snapshot history. That's
        // acceptable — the engine skips months with no paired snapshots.
        var fromUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc);
        var toUtc   = DateTime.SpecifyKind(today.AddDays(1), DateTimeKind.Utc);
        var snaps   = _supabase
            .GetAccountEquitySnapshotsInRangeAsync(acct.Login, acct.Source, fromUtc, toUtc)
            .GetAwaiter().GetResult();
        if (snaps.Count == 0) return list;

        // Pick the latest snapshot on or before each month-end (Beirut).
        // Month end = last day of each month at 00:00 Asia/Beirut → UTC.
        var monthAnchors = new List<DateTime>();
        var cursor = new DateTime(start.Year, start.Month, 1);
        while (cursor <= today)
        {
            var nextMonth = cursor.AddMonths(1);
            var monthEndLocal = DateTime.SpecifyKind(nextMonth, DateTimeKind.Unspecified);
            DateTime monthEndUtc;
            try { monthEndUtc = TimeZoneInfo.ConvertTimeToUtc(monthEndLocal, beirut); }
            catch (ArgumentException) { monthEndUtc = TimeZoneInfo.ConvertTimeToUtc(monthEndLocal.AddHours(1), beirut); }
            monthAnchors.Add(monthEndUtc);
            cursor = nextMonth;
        }

        decimal? prevEquity = null;
        foreach (var anchor in monthAnchors)
        {
            var snap = snaps.Where(s => s.SnapshotTime <= anchor).OrderByDescending(s => s.SnapshotTime).FirstOrDefault();
            if (snap == null) continue;
            if (prevEquity == null)
            {
                prevEquity = snap.Equity;
                continue;
            }
            var monthlyPl = snap.Equity - prevEquity.Value;
            list.Add((anchor, monthlyPl));
            prevEquity = snap.Equity;
        }

        return list;
    }

    /// <summary>
    /// Query the Python collector for LP trade flow (profit + commission + swap + fee
    /// across all OUT deals) over the Equity P&amp;L window. Used by the balance-reconciliation
    /// path on coverage rows because LP deals never reach Supabase.
    ///
    /// <para>Returns <c>(login, flow)</c> — login identifies which <c>trading_accounts.login</c>
    /// this flow applies to. Null login when the collector is unreachable or hasn't surfaced
    /// an account yet; callers fall back to 0 in that case (same behavior as before this fix).</para>
    /// </summary>
    private async Task<(long? Login, decimal Flow)> FetchCoverageTradeFlowAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct)
    {
        try
        {
            var http = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(10);

            // /deals treats `to` inclusively (adds 1 day internally); the request-
            // date range on this controller already shifts `toUtc` forward a day
            // for Beirut alignment, so we hand the collector the picker's raw
            // last day.
            var from = fromDate.ToString("yyyy-MM-dd");
            var to = toDate.ToString("yyyy-MM-dd");

            var dealsTask = http.GetStringAsync($"http://127.0.0.1:8100/deals?from={from}&to={to}", ct);
            var acctTask = http.GetStringAsync("http://127.0.0.1:8100/account", ct);
            await Task.WhenAll(dealsTask, acctTask);

            decimal flow = 0m;
            using (var doc = System.Text.Json.JsonDocument.Parse(dealsTask.Result))
            {
                if (doc.RootElement.TryGetProperty("debug", out var debug)
                    && debug.TryGetProperty("closedNet", out var net))
                {
                    flow = net.GetDecimal();
                }
            }

            long? login = null;
            using (var acctDoc = System.Text.Json.JsonDocument.Parse(acctTask.Result))
            {
                if (acctDoc.RootElement.TryGetProperty("login", out var loginEl)
                    && loginEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    login = loginEl.GetInt64();
                }
            }

            return (login, flow);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Coverage trade-flow fetch from collector failed — coverage NetDepW will fall back to 0 trade flow");
            return (null, 0m);
        }
    }
}
