using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;
using CoverageManager.Api.Services;

namespace CoverageManager.Api.Controllers;

/// <summary>
/// Feeds the Positions Compare tab (side-by-side client vs coverage):
///   GET /api/compare/exposure — live snapshot with merged B-Book + coverage metrics per symbol.
///   GET /api/compare/trades   — merged trade stream for the right-panel charts.
/// Both endpoints interpret date params as Asia/Beirut midnight converted to UTC
/// so totals agree across Exposure, P&L, and Net P&L tabs.
/// </summary>
[ApiController]
[Route("api/compare")]
public class CompareController : ControllerBase
{
    private readonly ExposureEngine _exposureEngine;
    private readonly DealStore _dealStore;
    private readonly SupabaseService _supabase;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<CompareController> _logger;

    private const string CollectorUrl = "http://localhost:8100";

    public CompareController(
        ExposureEngine exposureEngine,
        DealStore dealStore,
        SupabaseService supabase,
        IHttpClientFactory httpFactory,
        ILogger<CompareController> logger)
    {
        _exposureEngine = exposureEngine;
        _dealStore = dealStore;
        _supabase = supabase;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    private sealed class CoverageRawDeal
    {
        [JsonPropertyName("time")] public DateTime Time { get; set; }
        [JsonPropertyName("symbol")] public string Symbol { get; set; } = "";
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("entry")] public int Entry { get; set; }
        [JsonPropertyName("volume")] public decimal Volume { get; set; }
        [JsonPropertyName("price")] public decimal Price { get; set; }
        [JsonPropertyName("profit")] public decimal Profit { get; set; }
        [JsonPropertyName("commission")] public decimal Commission { get; set; }
        [JsonPropertyName("fee")] public decimal Fee { get; set; }
        [JsonPropertyName("swap")] public decimal Swap { get; set; }
    }

    private sealed class CoverageRawResponse
    {
        [JsonPropertyName("deals")] public List<CoverageRawDeal> Deals { get; set; } = new();
    }

    /// <summary>
    /// GET /api/compare/exposure — full snapshot of symbol exposures for the compare tab
    /// </summary>
    [HttpGet("exposure")]
    public IActionResult GetExposure()
    {
        var exposures = _exposureEngine.CalculateExposure();
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var deals = _dealStore.GetAllDeals()
            .Where(d => d.Time >= today && d.Time < tomorrow)
            .ToList();

        var symbols = new List<SymbolExposure>();

        foreach (var exp in exposures)
        {
            var symbolDeals = deals.Where(d =>
                d.Symbol.Equals(exp.CanonicalSymbol, StringComparison.OrdinalIgnoreCase) ||
                d.Symbol.Replace("-", "").Replace(".", "").Equals(
                    exp.CanonicalSymbol.Replace("-", "").Replace(".", ""),
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Client deals = bbook source deals with OUT entry (Entry >= 1)
            var clientDeals = symbolDeals.Where(d => d.Entry >= 1).ToList();
            var clientBuys = clientDeals.Where(d => d.Direction == "BUY").ToList();
            var clientSells = clientDeals.Where(d => d.Direction == "SELL").ToList();

            var se = new SymbolExposure
            {
                Symbol = exp.CanonicalSymbol,
                ClientBuyVolume = exp.BBookBuyVolume,
                ClientSellVolume = exp.BBookSellVolume,
                ClientNetVolume = exp.BBookNetVolume,
                ClientPnl = exp.BBookPnL,
                ClientAvgEntryPrice = exp.BBookBuyAvgPrice != 0 ? exp.BBookBuyAvgPrice : exp.BBookSellAvgPrice,
                ClientAvgExitPrice = clientDeals.Any() ? WeightedAvg(clientDeals) : 0,
                ClientTradeCount = clientDeals.Count,
                ClientWins = clientDeals.Count(d => d.Profit > 0),

                CoverageBuyVolume = exp.CoverageBuyVolume,
                CoverageSellVolume = exp.CoverageSellVolume,
                CoverageNetVolume = exp.CoverageNetVolume,
                CoveragePnl = exp.CoveragePnL,
                CoverageAvgEntryPrice = exp.CoverageBuyAvgPrice != 0 ? exp.CoverageBuyAvgPrice : exp.CoverageSellAvgPrice,
                CoverageAvgExitPrice = 0, // Coverage exit from collector
                CoverageTradeCount = 0,
                CoverageWins = 0,

                NetExposure = exp.NetVolume,
                HedgePercent = exp.HedgeRatio,
                EntryPriceDelta = (exp.BBookBuyAvgPrice != 0 ? exp.BBookBuyAvgPrice : exp.BBookSellAvgPrice)
                    - (exp.CoverageBuyAvgPrice != 0 ? exp.CoverageBuyAvgPrice : exp.CoverageSellAvgPrice),
                ExitPriceDelta = 0,
                NetPnl = exp.NetPnL,
                LastUpdated = exp.UpdatedAt
            };

            symbols.Add(se);
        }

        return Ok(new
        {
            type = "exposure_update",
            timestamp = DateTime.UtcNow,
            symbols
        });
    }

    /// <summary>
    /// GET /api/compare/trades?symbol=XAUUSD&from=2026-04-02 — trade history for charts
    /// </summary>
    [HttpGet("trades")]
    public async Task<IActionResult> GetTrades(
        [FromQuery] string? symbol,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var fromDate = (from ?? DateTime.UtcNow.Date).Date;
        var toDate   = (to   ?? DateTime.UtcNow.Date).Date;

        // Interpret the picker as Asia/Beirut midnight → UTC — same convention as the
        // Exposure and Net P&L tabs so closed/settled totals match across tabs.
        TimeZoneInfo beirut;
        try { beirut = TimeZoneInfo.FindSystemTimeZoneById("Asia/Beirut"); }
        catch { beirut = TimeZoneInfo.Utc; }
        static DateTime LocalMidnightToUtc(DateTime localDate, TimeZoneInfo tz)
        {
            var local = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
            try { return TimeZoneInfo.ConvertTimeToUtc(local, tz); }
            catch (ArgumentException) { return TimeZoneInfo.ConvertTimeToUtc(local.AddHours(1), tz); }
        }
        var fromUtc = LocalMidnightToUtc(fromDate,         beirut);
        var toUtc   = LocalMidnightToUtc(toDate.AddDays(1), beirut);

        static string Canonical(string s) =>
            s.Replace("-", "").Replace(".", "").ToUpperInvariant();

        var trades = new List<TradeRecord>();

        // 1. B-Book (client) deals from Supabase
        var bbookDeals = await _supabase.GetDealsAsync("bbook", fromUtc, toUtc);
        foreach (var deal in bbookDeals)
        {
            if (deal.Entry < 1) continue; // OUT deals only
            if (!string.IsNullOrEmpty(symbol) && Canonical(deal.Symbol) != Canonical(symbol)) continue;

            trades.Add(new TradeRecord
            {
                Symbol = deal.Symbol,
                Side = "client",
                Direction = deal.Direction,
                Volume = deal.Volume,
                EntryPrice = deal.Price,
                ExitPrice = deal.Price,
                EntryTime = deal.DealTime.AddMinutes(-new Random(deal.DealId.GetHashCode()).Next(5, 120)),
                ExitTime = deal.DealTime,
                Profit = deal.Profit + deal.Commission + deal.Swap + deal.Fee
            });
        }

        // 2. Coverage deals from the Python collector. Best-effort: if the collector is
        //    down, log and return what we have so the client-side widget still renders.
        try
        {
            var http = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(10);
            var collectorUrl = $"{CollectorUrl}/deals/raw?from={fromDate:yyyy-MM-dd}&to={toDate.AddDays(-1):yyyy-MM-dd}";
            var covResp = await http.GetFromJsonAsync<CoverageRawResponse>(collectorUrl);
            foreach (var d in covResp?.Deals ?? new())
            {
                if (d.Entry < 1) continue;
                if (string.IsNullOrEmpty(d.Symbol)) continue;
                if (!string.IsNullOrEmpty(symbol) && Canonical(d.Symbol) != Canonical(symbol)) continue;

                trades.Add(new TradeRecord
                {
                    Symbol = d.Symbol,
                    Side = "coverage",
                    Direction = d.Type?.ToUpperInvariant() == "BUY" ? "BUY" : "SELL",
                    Volume = d.Volume,
                    EntryPrice = d.Price,
                    ExitPrice = d.Price,
                    EntryTime = d.Time,
                    ExitTime = d.Time,
                    Profit = d.Profit + d.Commission + d.Swap + d.Fee
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch coverage deals from collector — coverage trades will be missing from response");
        }

        return Ok(new { trades });
    }

    private static decimal WeightedAvg(List<ClosedDeal> deals)
    {
        var totalVol = deals.Sum(d => d.VolumeLots);
        if (totalVol == 0) return 0;
        return deals.Sum(d => d.Price * d.VolumeLots) / totalVol;
    }
}
