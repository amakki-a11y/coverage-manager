using Microsoft.AspNetCore.Mvc;
using CoverageManager.Api.Services;
using CoverageManager.Core.Models.EquityPnL;

namespace CoverageManager.Api.Controllers;

/// <summary>
/// CRUD endpoints for Phase 2 per-group Equity P&amp;L configuration:
/// groups, membership, per-group rebate/PS rates, per-group spread rebates.
/// </summary>
[ApiController]
[Route("api/login-groups")]
public class LoginGroupsController : ControllerBase
{
    private readonly SupabaseService _supabase;
    private readonly ILogger<LoginGroupsController> _logger;

    public LoginGroupsController(SupabaseService supabase, ILogger<LoginGroupsController> logger)
    {
        _supabase = supabase;
        _logger = logger;
    }

    // ── Groups ────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var groups = await _supabase.GetLoginGroupsAsync();
        return Ok(groups);
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] LoginGroup g)
    {
        if (string.IsNullOrWhiteSpace(g.Name))
            return BadRequest(new { error = "name is required" });
        var saved = await _supabase.UpsertLoginGroupAsync(g);
        return saved != null ? Ok(saved) : StatusCode(500, new { error = "upsert failed" });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ok = await _supabase.DeleteLoginGroupAsync(id);
        return ok ? NoContent() : StatusCode(500);
    }

    // ── Membership ────────────────────────────────────────────
    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        var rows = await _supabase.GetLoginGroupMembersAsync(id);
        return Ok(rows);
    }

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] LoginGroupMember m)
    {
        if (m.Login <= 0 || string.IsNullOrWhiteSpace(m.Source))
            return BadRequest(new { error = "login and source are required" });
        m.GroupId = id;
        var ok = await _supabase.AddLoginGroupMemberAsync(m);
        return ok ? Ok(m) : StatusCode(500);
    }

    [HttpDelete("{id:guid}/members")]
    public async Task<IActionResult> RemoveMember(Guid id, [FromQuery] long login, [FromQuery] string source)
    {
        if (login <= 0 || string.IsNullOrWhiteSpace(source))
            return BadRequest(new { error = "login and source are required" });
        var ok = await _supabase.RemoveLoginGroupMemberAsync(id, login, source);
        return ok ? NoContent() : StatusCode(500);
    }

    // ── Group-level rebate / PS config ────────────────────────
    [HttpGet("config")]
    public async Task<IActionResult> GetConfigs()
    {
        var rows = await _supabase.GetGroupConfigsAsync();
        return Ok(rows);
    }

    [HttpPut("config")]
    public async Task<IActionResult> UpsertConfig([FromBody] EquityPnLGroupConfig cfg)
    {
        if (cfg.GroupId == Guid.Empty)
            return BadRequest(new { error = "group_id is required" });
        var ok = await _supabase.UpsertGroupConfigAsync(cfg);
        return ok ? Ok(cfg) : StatusCode(500);
    }

    // ── Group-level spread rebate rates ───────────────────────
    [HttpGet("{id:guid}/spread-rebates")]
    public async Task<IActionResult> GetSpreadRebates(Guid id)
    {
        var rows = await _supabase.GetGroupSpreadRebateRatesAsync(id);
        return Ok(rows);
    }

    [HttpPut("{id:guid}/spread-rebates")]
    public async Task<IActionResult> UpsertSpreadRebates(Guid id, [FromBody] List<GroupSpreadRebateRate> rates)
    {
        if (rates == null || rates.Count == 0)
            return BadRequest(new { error = "at least one rate required" });
        // Force group_id on every row to match the path — prevents cross-group writes.
        foreach (var r in rates) r.GroupId = id;
        var ok = await _supabase.UpsertGroupSpreadRebateRatesAsync(rates);
        return ok ? Ok(new { upserted = rates.Count }) : StatusCode(500);
    }

    [HttpDelete("{id:guid}/spread-rebates")]
    public async Task<IActionResult> DeleteSpreadRebate(Guid id, [FromQuery] string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return BadRequest(new { error = "symbol is required" });
        var ok = await _supabase.DeleteGroupSpreadRebateRateAsync(id, symbol);
        return ok ? NoContent() : StatusCode(500);
    }
}
