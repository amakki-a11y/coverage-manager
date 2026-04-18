using Microsoft.AspNetCore.Mvc;
using CoverageManager.Api.Services;
using CoverageManager.Core.Models;

namespace CoverageManager.Api.Controllers;

/// <summary>
/// CRUD for the dealer-configurable snapshot schedules driving the Period P&L feature.
///   GET    /api/snapshot-schedules              — list all
///   POST   /api/snapshot-schedules              — create (body: SnapshotSchedule)
///   PUT    /api/snapshot-schedules/{id}         — update (body: SnapshotSchedule)
///   DELETE /api/snapshot-schedules/{id}         — delete
///   POST   /api/snapshot-schedules/{id}/run-now — capture immediately using this schedule's cadence label
/// </summary>
[ApiController]
[Route("api/snapshot-schedules")]
public class SnapshotSchedulesController : ControllerBase
{
    private readonly SupabaseService _supabase;
    private readonly ExposureSnapshotService _snapshotService;
    private readonly ILogger<SnapshotSchedulesController> _logger;

    public SnapshotSchedulesController(
        SupabaseService supabase,
        ExposureSnapshotService snapshotService,
        ILogger<SnapshotSchedulesController> logger)
    {
        _supabase = supabase;
        _snapshotService = snapshotService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var schedules = await _supabase.GetSnapshotSchedulesAsync();
        return Ok(schedules);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SnapshotSchedule schedule)
    {
        if (string.IsNullOrWhiteSpace(schedule.Name)) return BadRequest(new { error = "Name is required" });
        schedule.Id = null; // force create
        var created = await _supabase.UpsertSnapshotScheduleAsync(schedule);
        if (created == null) return StatusCode(500, new { error = "Failed to create schedule" });
        return Ok(created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SnapshotSchedule schedule)
    {
        schedule.Id = id;
        var updated = await _supabase.UpsertSnapshotScheduleAsync(schedule);
        if (updated == null) return StatusCode(500, new { error = "Failed to update schedule" });
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ok = await _supabase.DeleteSnapshotScheduleAsync(id);
        if (!ok) return StatusCode(500, new { error = "Failed to delete schedule" });
        return NoContent();
    }

    [HttpPost("{id:guid}/run-now")]
    public async Task<IActionResult> RunNow(Guid id, CancellationToken ct)
    {
        var schedules = await _supabase.GetSnapshotSchedulesAsync();
        var s = schedules.FirstOrDefault(x => x.Id == id);
        if (s == null) return NotFound();
        var count = await _snapshotService.RunNowAsync(triggerType: s.Cadence, label: s.Name, ct);
        // Also update the schedule's last_run_at so the UI shows fresh.
        s.LastRunAt = DateTime.UtcNow;
        await _supabase.UpsertSnapshotScheduleAsync(s);
        return Ok(new { captured = count, at = DateTime.UtcNow });
    }
}
