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

    public ExposureController(
        ExposureEngine exposureEngine,
        PositionManager positionManager,
        MT5ManagerConnection mt5Connection,
        DealStore dealStore,
        SupabaseService supabase)
    {
        _exposureEngine = exposureEngine;
        _positionManager = positionManager;
        _mt5Connection = mt5Connection;
        _dealStore = dealStore;
        _supabase = supabase;
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
        var fromDate = from ?? DateTime.UtcNow.Date;
        var toDate = (to ?? DateTime.UtcNow.Date).AddDays(1); // Include full end day

        // Query Supabase for the date range (has full history)
        var deals = await _supabase.GetDealsAsync("bbook", fromDate, toDate);
        var movedLogins = await _supabase.GetMovedLoginsAsync();

        var tradeDeals = deals
            .Where(d => d.Action <= 1) // Exclude balance/credit (Action >= 2)
            .Where(d => !string.IsNullOrEmpty(d.Symbol))
            .Where(d => !movedLogins.Contains(d.Login)) // Exclude moved accounts
            .ToList();

        var symbols = tradeDeals
            .GroupBy(d => d.Symbol)
            .Select(g =>
            {
                var outDeals = g.Where(d => d.Entry == 1 || d.Entry == 2 || d.Entry == 3).ToList();
                return new SymbolPnL
                {
                    Symbol = g.Key,
                    DealCount = g.Count(),
                    TotalProfit = outDeals.Sum(d => d.Profit),
                    TotalCommission = g.Sum(d => d.Commission),
                    TotalSwap = outDeals.Sum(d => d.Swap),
                    TotalFee = g.Sum(d => d.Fee),
                    TotalVolume = g.Sum(d => d.Volume),
                    BuyVolume = g.Where(d => d.Direction == "BUY").Sum(d => d.Volume),
                    SellVolume = g.Where(d => d.Direction == "SELL").Sum(d => d.Volume)
                };
            })
            .OrderByDescending(p => Math.Abs(p.NetPnL))
            .ToList();

        return Ok(new
        {
            totalDeals = tradeDeals.Count,
            symbols,
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
        [FromQuery] bool fix = false)
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

        // Fix: find deals in MT5 but not in Supabase, upsert them
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
                    DealTime = d.Time
                })
                .ToList();

            if (missingDeals.Count > 0)
            {
                await _supabase.UpsertDealsAsync(missingDeals);
                fixedCount = missingDeals.Count;
            }
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
            elapsed = sw.Elapsed.ToString(@"hh\:mm\:ss")
        });
    }
}
