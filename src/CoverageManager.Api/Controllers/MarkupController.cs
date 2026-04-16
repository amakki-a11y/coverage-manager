using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using CoverageManager.Api.Services;

namespace CoverageManager.Api.Controllers;

/// <summary>
/// GET /api/markup/match — Aggregates client (bbook) deals vs coverage deals per
/// canonical symbol and computes the broker's mark-up profit from the VWAP price spread.
/// </summary>
[ApiController]
[Route("api/markup")]
public class MarkupController : ControllerBase
{
    private readonly SupabaseService _supabase;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<MarkupController> _logger;

    private const string CollectorUrl = "http://localhost:8100";

    public MarkupController(
        SupabaseService supabase,
        IHttpClientFactory httpFactory,
        ILogger<MarkupController> logger)
    {
        _supabase = supabase;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    private sealed class CoverageRawDeal
    {
        [JsonPropertyName("ticket")] public long Ticket { get; set; }
        [JsonPropertyName("time")] public DateTime Time { get; set; }
        [JsonPropertyName("timeMsc")] public long TimeMsc { get; set; }
        [JsonPropertyName("symbol")] public string Symbol { get; set; } = "";
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("entry")] public int Entry { get; set; }
        [JsonPropertyName("volume")] public decimal Volume { get; set; }
        [JsonPropertyName("price")] public decimal Price { get; set; }
        [JsonPropertyName("profit")] public decimal Profit { get; set; }
        [JsonPropertyName("commission")] public decimal Commission { get; set; }
        [JsonPropertyName("fee")] public decimal Fee { get; set; }
        [JsonPropertyName("swap")] public decimal Swap { get; set; }
        [JsonPropertyName("positionId")] public long PositionId { get; set; }
        [JsonPropertyName("comment")] public string Comment { get; set; } = "";
    }

    private sealed class CoverageRawResponse
    {
        [JsonPropertyName("deals")] public List<CoverageRawDeal> Deals { get; set; } = new();
        [JsonPropertyName("count")] public int Count { get; set; }
    }

    [HttpGet("match")]
    public async Task<IActionResult> Match(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var fromDate = from ?? DateTime.UtcNow.Date;
            var toDate = (to ?? DateTime.UtcNow.Date).AddDays(1);

            // 1. Fetch bbook deals from Supabase
            var bbookDeals = await _supabase.GetDealsAsync("bbook", fromDate, toDate);
            var movedLogins = await _supabase.GetMovedLoginsAsync();

            // Filter: trade deals only (action 0/1), non-empty symbol, not moved accounts
            // Use OUT deals only (entry 1/2/3) for P&L realization - IN deals are opening
            var bbookTrade = bbookDeals
                .Where(d => d.Action <= 1 && !string.IsNullOrEmpty(d.Symbol))
                .Where(d => !movedLogins.Contains(d.Login))
                .Where(d => d.Entry == 1 || d.Entry == 2 || d.Entry == 3)
                .ToList();

            // 2. Fetch coverage raw deals from Python collector
            var http = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(30);
            var collectorUrl = $"{CollectorUrl}/deals/raw?from={fromDate:yyyy-MM-dd}&to={toDate.AddDays(-1):yyyy-MM-dd}";
            CoverageRawResponse? covResp;
            try
            {
                covResp = await http.GetFromJsonAsync<CoverageRawResponse>(collectorUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch coverage raw deals from collector at {Url}", collectorUrl);
                return StatusCode(503, new { error = $"Collector unreachable: {ex.Message}" });
            }

            var covDeals = (covResp?.Deals ?? new())
                .Where(d => d.Entry == 1 || d.Entry == 2 || d.Entry == 3)
                .Where(d => !string.IsNullOrEmpty(d.Symbol))
                .ToList();

            // 3. Fetch symbol mappings and build lookup
            var mappings = await _supabase.GetMappingsAsync();
            var bbookToCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var covToCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var canonicalContractSize = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in mappings)
            {
                if (!string.IsNullOrEmpty(m.BBookSymbol))
                    bbookToCanonical[m.BBookSymbol] = m.CanonicalName.ToUpperInvariant();
                if (!string.IsNullOrEmpty(m.CoverageSymbol))
                    covToCanonical[m.CoverageSymbol] = m.CanonicalName.ToUpperInvariant();
                canonicalContractSize[m.CanonicalName.ToUpperInvariant()] =
                    m.BBookContractSize > 0 ? m.BBookContractSize : 1;
            }

            static string StripSuffix(string sym)
            {
                if (string.IsNullOrEmpty(sym)) return sym;
                var s = sym.TrimEnd('-').TrimEnd('.');
                // Remove trailing .c, .r, .m suffixes used by broker mapping schemes
                if (s.EndsWith(".c", StringComparison.OrdinalIgnoreCase)
                    || s.EndsWith(".r", StringComparison.OrdinalIgnoreCase)
                    || s.EndsWith(".m", StringComparison.OrdinalIgnoreCase))
                    s = s[..^2];
                return s.ToUpperInvariant();
            }

            string CanBbook(string sym) =>
                bbookToCanonical.TryGetValue(sym, out var c) ? c : StripSuffix(sym);
            string CanCov(string sym) =>
                covToCanonical.TryGetValue(sym, out var c) ? c : StripSuffix(sym);

            // 4. Aggregate per canonical symbol per direction
            var agg = new Dictionary<string, SymbolAgg>(StringComparer.OrdinalIgnoreCase);

            foreach (var b in bbookTrade)
            {
                var can = CanBbook(b.Symbol);
                if (!agg.TryGetValue(can, out var a))
                    agg[can] = a = new SymbolAgg { Symbol = can };
                a.ClientDealCount++;
                if (b.Direction.Equals("BUY", StringComparison.OrdinalIgnoreCase))
                {
                    a.ClientBuyVol += b.Volume;
                    a.ClientBuyNotional += b.Price * b.Volume;
                }
                else
                {
                    a.ClientSellVol += b.Volume;
                    a.ClientSellNotional += b.Price * b.Volume;
                }
                a.ClientProfit += b.Profit + b.Commission + b.Swap + b.Fee;
            }

            foreach (var c in covDeals)
            {
                var can = CanCov(c.Symbol);
                if (!agg.TryGetValue(can, out var a))
                    agg[can] = a = new SymbolAgg { Symbol = can };
                a.CoverageDealCount++;
                if (c.Type.Equals("buy", StringComparison.OrdinalIgnoreCase))
                {
                    a.CoverageBuyVol += c.Volume;
                    a.CoverageBuyNotional += c.Price * c.Volume;
                }
                else
                {
                    a.CoverageSellVol += c.Volume;
                    a.CoverageSellNotional += c.Price * c.Volume;
                }
                a.CoverageProfit += c.Profit + c.Commission + c.Swap + c.Fee;
            }

            // 5. Compute per-symbol markup
            // The broker's real mark-up IS the net P&L from both books combined:
            //   markup = -clientPnL + coveragePnL
            // (client P&L inverted because broker is the counterparty)
            //
            // The VWAP price spread is shown as informational (avg price edge per lot).
            // We do NOT multiply by contract size because the mappings are unreliable —
            // the broker net P&L already accounts for contract size via MT5's profit calc.
            var results = new List<ResultRow>();
            foreach (var kv in agg)
            {
                var a = kv.Value;
                decimal cliAvgBuy = a.ClientBuyVol > 0 ? a.ClientBuyNotional / a.ClientBuyVol : 0;
                decimal cliAvgSell = a.ClientSellVol > 0 ? a.ClientSellNotional / a.ClientSellVol : 0;
                decimal covAvgBuy = a.CoverageBuyVol > 0 ? a.CoverageBuyNotional / a.CoverageBuyVol : 0;
                decimal covAvgSell = a.CoverageSellVol > 0 ? a.CoverageSellNotional / a.CoverageSellVol : 0;

                // Price edge per lot (informational — shows the spread broker is capturing)
                // BUY side: client paid this much more than LP
                // SELL side: client sold for this much less than LP
                decimal priceEdgeBuy = cliAvgBuy > 0 && covAvgBuy > 0 ? cliAvgBuy - covAvgBuy : 0;
                decimal priceEdgeSell = cliAvgSell > 0 && covAvgSell > 0 ? covAvgSell - cliAvgSell : 0;

                // Markup = broker net P&L per symbol (source of truth)
                decimal markup = -a.ClientProfit + a.CoverageProfit;

                results.Add(new ResultRow
                {
                    symbol = kv.Key,
                    client = new BookSide
                    {
                        deals = a.ClientDealCount,
                        buyVol = Math.Round(a.ClientBuyVol, 2),
                        sellVol = Math.Round(a.ClientSellVol, 2),
                        avgBuy = Math.Round(cliAvgBuy, 5),
                        avgSell = Math.Round(cliAvgSell, 5),
                        profit = Math.Round(a.ClientProfit, 2)
                    },
                    coverage = new BookSide
                    {
                        deals = a.CoverageDealCount,
                        buyVol = Math.Round(a.CoverageBuyVol, 2),
                        sellVol = Math.Round(a.CoverageSellVol, 2),
                        avgBuy = Math.Round(covAvgBuy, 5),
                        avgSell = Math.Round(covAvgSell, 5),
                        profit = Math.Round(a.CoverageProfit, 2)
                    },
                    priceEdgeBuy = Math.Round(priceEdgeBuy, 5),
                    priceEdgeSell = Math.Round(priceEdgeSell, 5),
                    markup = Math.Round(markup, 2),
                    hedgeRatioBuy = a.ClientBuyVol > 0 ? Math.Round(100m * a.CoverageBuyVol / a.ClientBuyVol, 1) : 0m,
                    hedgeRatioSell = a.ClientSellVol > 0 ? Math.Round(100m * a.CoverageSellVol / a.ClientSellVol, 1) : 0m
                });
            }

            var sorted = results.OrderByDescending(x => Math.Abs(x.markup)).ToList();
            var totalMarkup = sorted.Sum(x => x.markup);

            sw.Stop();

            return Ok(new
            {
                from = fromDate.ToString("yyyy-MM-dd"),
                to = toDate.AddDays(-1).ToString("yyyy-MM-dd"),
                summary = new
                {
                    totalMarkup = Math.Round(totalMarkup, 2),
                    clientDealsTotal = bbookTrade.Count,
                    coverageDealsTotal = covDeals.Count,
                    symbolsCount = sorted.Count,
                    clientProfitTotal = Math.Round(agg.Values.Sum(a => a.ClientProfit), 2),
                    coverageProfitTotal = Math.Round(agg.Values.Sum(a => a.CoverageProfit), 2)
                },
                symbols = sorted,
                elapsed = sw.Elapsed.ToString(@"hh\:mm\:ss\.fff")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Markup match failed");
            return StatusCode(500, new { error = $"Markup match failed: {ex.Message}" });
        }
    }

    private sealed class SymbolAgg
    {
        public string Symbol { get; set; } = "";
        public int ClientDealCount { get; set; }
        public int CoverageDealCount { get; set; }
        public decimal ClientBuyVol { get; set; }
        public decimal ClientSellVol { get; set; }
        public decimal ClientBuyNotional { get; set; }
        public decimal ClientSellNotional { get; set; }
        public decimal CoverageBuyVol { get; set; }
        public decimal CoverageSellVol { get; set; }
        public decimal CoverageBuyNotional { get; set; }
        public decimal CoverageSellNotional { get; set; }
        public decimal ClientProfit { get; set; }
        public decimal CoverageProfit { get; set; }
    }

    public sealed class BookSide
    {
        public int deals { get; set; }
        public decimal buyVol { get; set; }
        public decimal sellVol { get; set; }
        public decimal avgBuy { get; set; }
        public decimal avgSell { get; set; }
        public decimal profit { get; set; }
    }

    public sealed class ResultRow
    {
        public string symbol { get; set; } = "";
        public BookSide client { get; set; } = new();
        public BookSide coverage { get; set; } = new();
        public decimal priceEdgeBuy { get; set; }
        public decimal priceEdgeSell { get; set; }
        public decimal markup { get; set; }
        public decimal hedgeRatioBuy { get; set; }
        public decimal hedgeRatioSell { get; set; }
    }
}
