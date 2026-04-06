using Microsoft.AspNetCore.Mvc;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;
using CoverageManager.Api.Services;

namespace CoverageManager.Api.Controllers;

[ApiController]
[Route("api/alerts")]
public class AlertsController : ControllerBase
{
    private readonly AlertEngine _alertEngine;
    private readonly SupabaseService _supabase;
    private readonly ExposureBroadcastService _broadcast;

    public AlertsController(AlertEngine alertEngine, SupabaseService supabase, ExposureBroadcastService broadcast)
    {
        _alertEngine = alertEngine;
        _supabase = supabase;
        _broadcast = broadcast;
    }

    /// <summary>
    /// GET /api/alerts — active in-memory alerts
    /// </summary>
    [HttpGet]
    public IActionResult GetAlerts([FromQuery] bool unacknowledgedOnly = false)
    {
        var alerts = unacknowledgedOnly
            ? _alertEngine.GetUnacknowledgedAlerts()
            : _alertEngine.GetActiveAlerts();

        return Ok(new
        {
            count = alerts.Count,
            unacknowledged = _alertEngine.ActiveAlertCount,
            alerts
        });
    }

    /// <summary>
    /// GET /api/alerts/history — persisted alert events from Supabase
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetAlertHistory([FromQuery] bool unacknowledgedOnly = false, [FromQuery] int limit = 100)
    {
        var events = await _supabase.GetAlertEventsAsync(unacknowledgedOnly, limit);
        return Ok(new { count = events.Count, events });
    }

    /// <summary>
    /// POST /api/alerts/{id}/acknowledge — mark alert as acknowledged
    /// </summary>
    [HttpPost("{id}/acknowledge")]
    public async Task<IActionResult> AcknowledgeAlert(Guid id)
    {
        var inMemory = _alertEngine.AcknowledgeAlert(id);
        var inDb = await _supabase.AcknowledgeAlertEventAsync(id);

        if (!inMemory && !inDb)
            return NotFound(new { message = "Alert not found" });

        return Ok(new { message = "Alert acknowledged" });
    }

    // ── Alert Rules (Thresholds) CRUD ──

    /// <summary>
    /// GET /api/alerts/rules — all configured alert rules
    /// </summary>
    [HttpGet("rules")]
    public async Task<IActionResult> GetRules()
    {
        var rules = await _supabase.GetAlertRulesAsync();
        return Ok(new { count = rules.Count, rules });
    }

    /// <summary>
    /// POST /api/alerts/rules — create or update an alert rule
    /// </summary>
    [HttpPost("rules")]
    public async Task<IActionResult> UpsertRule([FromBody] RiskThreshold rule)
    {
        if (string.IsNullOrEmpty(rule.TriggerType))
            return BadRequest(new { message = "trigger_type is required" });

        var result = await _supabase.UpsertAlertRuleAsync(rule);
        if (result == null)
            return StatusCode(500, new { message = "Failed to save alert rule" });

        // Reload thresholds into engine
        var allRules = await _supabase.GetAlertRulesAsync();
        _alertEngine.LoadThresholds(allRules);
        _broadcast.MarkDirty();

        return Ok(result);
    }

    /// <summary>
    /// DELETE /api/alerts/rules/{id} — delete an alert rule
    /// </summary>
    [HttpDelete("rules/{id}")]
    public async Task<IActionResult> DeleteRule(Guid id)
    {
        var success = await _supabase.DeleteAlertRuleAsync(id);
        if (!success)
            return StatusCode(500, new { message = "Failed to delete alert rule" });

        // Reload thresholds into engine
        var allRules = await _supabase.GetAlertRulesAsync();
        _alertEngine.LoadThresholds(allRules);
        _broadcast.MarkDirty();

        return Ok(new { message = "Rule deleted" });
    }
}
