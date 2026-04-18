using Microsoft.AspNetCore.Mvc;
using CoverageManager.Api.Services;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models.Bridge;

namespace CoverageManager.Api.Controllers;

/// <summary>
/// Phase 2.5 — Bridge Execution Analysis endpoints.
///   GET /api/bridge/executions — persisted pairs for a date range (initial load / filter changes).
///   GET /api/bridge/live — current in-memory pairs (live tail, last ~24h).
///   GET /api/bridge/health — Centroid feed state.
/// Real-time updates are delivered via /ws/bridge (see Program.cs).
/// </summary>
[ApiController]
[Route("api/bridge")]
public class BridgeController : ControllerBase
{
    private readonly ICentroidBridgeService _feed;
    private readonly BridgeExecutionStore _store;
    private readonly BridgeSupabaseWriter _writer;
    private readonly DealStore _dealStore;
    private readonly CoverageDealIndex _coverageIndex;
    private readonly ILogger<BridgeController> _logger;

    public BridgeController(
        ICentroidBridgeService feed,
        BridgeExecutionStore store,
        BridgeSupabaseWriter writer,
        DealStore dealStore,
        CoverageDealIndex coverageIndex,
        ILogger<BridgeController> logger)
    {
        _feed = feed;
        _store = store;
        _writer = writer;
        _dealStore = dealStore;
        _coverageIndex = coverageIndex;
        _logger = logger;
    }

    /// <summary>
    /// Enrich each CLIENT pair with the true B-Book MT5 deal number by looking up
    /// (ext_login, ext_order) against DealStore. Safe to call before serialization —
    /// mutates ClientMtDealId only when null, so we don't overwrite a value we already have.
    /// </summary>
    private void EnrichWithRealDealIds(IEnumerable<ExecutionPair> pairs)
    {
        foreach (var p in pairs)
        {
            // CLIENT side: (ext_login, ext_order) on the B-Book -> MT5 deal ticket from DealStore.
            if (!p.ClientMtDealId.HasValue &&
                p.ClientMtLogin.HasValue && p.ClientMtTicket.HasValue)
            {
                var dealId = _dealStore.GetDealIdByOrder(p.ClientMtLogin.Value, p.ClientMtTicket.Value);
                if (dealId.HasValue) p.ClientMtDealId = dealId;
            }

            // COV OUT side: FXGROW writes Centroid's maker_order_id into MT5 external_id on the
            // 96900 account. Resolve maker_order_id -> real coverage MT5 deal ticket.
            foreach (var c in p.CovFills)
            {
                if (c.MtDealId.HasValue) continue;
                if (!ulong.TryParse(c.MakerOrderId, out var makerId) || makerId == 0) continue;
                var covTicket = _coverageIndex.GetDealIdByMakerOrderId(makerId);
                if (covTicket.HasValue) c.MtDealId = covTicket;
            }

            // CLIENT markup detail from Centroid's orders_report endpoint (keyed by cen_ord_id).
            if (!string.IsNullOrEmpty(p.CenOrdId))
            {
                var d = _feed.GetClientDetail(p.CenOrdId);
                if (d != null)
                {
                    p.ClientReqPrice        ??= d.ReqAvgPrice;
                    p.ClientTotalMarkup     ??= d.TotalMarkup;
                    p.ClientExtMarkup       ??= d.ExtMarkup;
                    p.ClientAFillVolume     ??= d.AFillVolume;
                    p.ClientBFillVolume     ??= d.BFillVolume;
                    p.ClientAAvgPrice       ??= d.AAvgPrice;
                    p.ClientBAvgPrice       ??= d.BAvgPrice;
                }
            }
        }
    }

    [HttpGet("executions")]
    public async Task<IActionResult> GetExecutions(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? symbol = null,
        [FromQuery] int limit = 500,
        CancellationToken ct = default)
    {
        try
        {
            // Interpret date-only query params as UTC midnight (don't let local TZ slide them).
            // `to` is inclusive-of-day — add 1 day so "today..today" = full 24h window.
            var fromUtc = DateTime.SpecifyKind(from ?? DateTime.UtcNow.Date, DateTimeKind.Utc);
            var toUtcDay = DateTime.SpecifyKind(to ?? DateTime.UtcNow.Date, DateTimeKind.Utc);
            var toUtc = toUtcDay.AddDays(1);
            var canonical = string.IsNullOrWhiteSpace(symbol) ? null : symbol.Trim().ToUpperInvariant();

            // Prefer persisted rows; fall back to live store if DB returns empty (e.g. fresh session).
            var rows = await _writer.QueryAsync(fromUtc, toUtc, canonical, limit, ct);
            if (rows.Count == 0)
            {
                rows = _store.Query(fromUtc, toUtc, canonical, limit).ToList();
            }

            EnrichWithRealDealIds(rows);

            return Ok(new
            {
                from = fromUtc,
                to = toUtc,
                symbol = canonical,
                count = rows.Count,
                pairs = rows,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET /api/bridge/executions failed");
            return StatusCode(500, new { error = "Internal error" });
        }
    }

    [HttpGet("live")]
    public IActionResult GetLive([FromQuery] int limit = 500)
    {
        try
        {
            var pairs = _store.Snapshot().Take(Math.Clamp(limit, 1, 5000)).ToList();
            EnrichWithRealDealIds(pairs);
            return Ok(new { count = pairs.Count, pairs });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET /api/bridge/live failed");
            return StatusCode(500, new { error = "Internal error" });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        try
        {
            var health = _feed.GetHealth();
            return Ok(new
            {
                mode = health.Mode,
                state = health.State.ToString(),
                lastMessageUtc = health.LastMessageUtc,
                messagesReceived = health.MessagesReceived,
                lastError = health.LastError,
                pairsInMemory = _store.Count,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET /api/bridge/health failed");
            return StatusCode(500, new { error = "Internal error" });
        }
    }
}
