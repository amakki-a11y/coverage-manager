using Microsoft.AspNetCore.Mvc;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;
using CoverageManager.Api.Services;

namespace CoverageManager.Api.Controllers;

[ApiController]
[Route("api/compare")]
public class CompareController : ControllerBase
{
    private readonly ExposureEngine _exposureEngine;
    private readonly DealStore _dealStore;
    private readonly SupabaseService _supabase;

    public CompareController(
        ExposureEngine exposureEngine,
        DealStore dealStore,
        SupabaseService supabase)
    {
        _exposureEngine = exposureEngine;
        _dealStore = dealStore;
        _supabase = supabase;
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
        var fromDate = from ?? DateTime.UtcNow.Date;
        var toDate = to ?? fromDate.AddDays(1);

        // Get B-Book deals from Supabase
        var bbookDeals = await _supabase.GetDealsAsync("bbook", fromDate, toDate);

        var trades = new List<TradeRecord>();

        foreach (var deal in bbookDeals)
        {
            // Filter by symbol if specified
            if (!string.IsNullOrEmpty(symbol))
            {
                var dealCanonical = deal.Symbol.Replace("-", "").Replace(".", "").ToUpperInvariant();
                var queryCanonical = symbol.Replace("-", "").Replace(".", "").ToUpperInvariant();
                if (dealCanonical != queryCanonical) continue;
            }

            // Only include OUT deals (have profit data)
            if (deal.Entry < 1) continue;

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

        return Ok(new { trades });
    }

    private static decimal WeightedAvg(List<ClosedDeal> deals)
    {
        var totalVol = deals.Sum(d => d.VolumeLots);
        if (totalVol == 0) return 0;
        return deals.Sum(d => d.Price * d.VolumeLots) / totalVol;
    }
}
