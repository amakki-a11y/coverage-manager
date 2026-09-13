using Microsoft.AspNetCore.Mvc;
using CoverageManager.Api.Services;

namespace CoverageManager.Api.Controllers;

/// <summary>
/// Surfaces the v2 feed/store self-check to the UI (Settings -> Data Integrity -> Reconciliation).
/// Routes are unchanged from v1 so the card keeps working; they are now backed by
/// <see cref="FeedStoreSelfCheckService"/>. v1's heavy ReconciliationService sweep was deleted.
///   GET  /api/reconciliation/status   — last N runs (self-check runs, newest first)
///   POST /api/reconciliation/run      — run a self-check now. The window is always the feed's
///                                       retained window; fromUtc/toUtc in the body are accepted
///                                       for compatibility and ignored.
/// </summary>
[ApiController]
[Route("api/reconciliation")]
public class ReconciliationController : ControllerBase
{
    private readonly FeedStoreSelfCheckService _service;
    private readonly IDataStore _supabase;
    private readonly ILogger<ReconciliationController> _logger;

    public ReconciliationController(
        FeedStoreSelfCheckService service,
        IDataStore supabase,
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

    /// <summary>Body for <see cref="RunNow"/>. Kept for compatibility with the v1 card; the bounds are ignored.</summary>
    public sealed class RunRequest
    {
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
    }

    /// <summary>
    /// POST /api/reconciliation/run — run a self-check now over the feed's retained window. Returns
    /// the recorded run: feed/store counts, missing and modified deals re-written, ghost_deleted 0.
    /// </summary>
    [HttpPost("run")]
    public async Task<IActionResult> RunNow([FromBody] RunRequest? body, CancellationToken ct)
    {
        var run = await _service.RunNowAsync("manual", ct);
        return Ok(run);
    }
}
