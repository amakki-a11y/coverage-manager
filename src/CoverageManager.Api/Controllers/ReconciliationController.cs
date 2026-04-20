using Microsoft.AspNetCore.Mvc;
using CoverageManager.Api.Services;

namespace CoverageManager.Api.Controllers;

/// <summary>
/// Surfaces the deal-reconciliation sweep to the UI (Settings tab).
///   GET  /api/reconciliation/status         — last N runs (history for the settings table)
///   POST /api/reconciliation/run            — trigger a sweep now, body { fromUtc?, toUtc? }
/// </summary>
[ApiController]
[Route("api/reconciliation")]
public class ReconciliationController : ControllerBase
{
    private readonly ReconciliationService _service;
    private readonly SupabaseService _supabase;
    private readonly ILogger<ReconciliationController> _logger;

    public ReconciliationController(
        ReconciliationService service,
        SupabaseService supabase,
        ILogger<ReconciliationController> logger)
    {
        _service = service;
        _supabase = supabase;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/reconciliation/status?limit=30 — recent reconciliation runs,
    /// newest first. Feeds the Settings → Data Integrity → Reconciliation history table.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status([FromQuery] int limit = 30)
    {
        var runs = await _supabase.ListReconciliationRunsAsync(limit);
        return Ok(new { runs, count = runs.Count });
    }

    /// <summary>Body for <see cref="RunNow"/>. Both bounds optional; defaults to last 14 days when omitted.</summary>
    public sealed class RunRequest
    {
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
    }

    /// <summary>
    /// POST /api/reconciliation/run — trigger a sweep immediately. Returns the completed
    /// run record including mt5/supabase counts, backfilled, ghost-deleted, modified.
    /// </summary>
    [HttpPost("run")]
    public async Task<IActionResult> RunNow([FromBody] RunRequest? body, CancellationToken ct)
    {
        var run = await _service.RunNowAsync(body?.FromUtc, body?.ToUtc, ct);
        return Ok(run);
    }
}
