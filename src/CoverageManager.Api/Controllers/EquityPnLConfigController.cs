using Microsoft.AspNetCore.Mvc;
using CoverageManager.Api.Services;
using CoverageManager.Core.Models.EquityPnL;

namespace CoverageManager.Api.Controllers;

/// <summary>
/// CRUD endpoints for the per-login Equity P&amp;L configuration: commission
/// rebate %, profit-share %, PS contract start date, and per-(login, symbol)
/// spread rebate rates.
///
/// <para>All writes upsert — PK is <c>(login, source)</c> for client config
/// and <c>(login, source, canonical_symbol)</c> for spread rebates, so the
/// dealer can edit a row by PUT-ing the full replacement body.</para>
/// </summary>
[ApiController]
[Route("api/equity-pnl-config")]
public class EquityPnLConfigController : ControllerBase
{
    private readonly SupabaseService _supabase;
    private readonly ILogger<EquityPnLConfigController> _logger;

    public EquityPnLConfigController(SupabaseService supabase, ILogger<EquityPnLConfigController> logger)
    {
        _supabase = supabase;
        _logger = logger;
    }

    /// <summary>GET /api/equity-pnl-config — all per-login configs.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? source = null)
    {
        var rows = await _supabase.GetEquityPnLClientConfigsAsync(source);
        return Ok(rows);
    }

    /// <summary>PUT /api/equity-pnl-config — upsert one config row.</summary>
    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] EquityPnLClientConfig cfg)
    {
        if (cfg.Login <= 0 || string.IsNullOrEmpty(cfg.Source))
            return BadRequest(new { error = "login and source are required" });

        var ok = await _supabase.UpsertEquityPnLClientConfigAsync(cfg);
        return ok ? Ok(cfg) : StatusCode(500, new { error = "upsert failed" });
    }

    /// <summary>GET /api/equity-pnl-config/spread-rebates — optionally filtered by login.</summary>
    [HttpGet("spread-rebates")]
    public async Task<IActionResult> GetSpreadRebates([FromQuery] long? login = null)
    {
        var rows = await _supabase.GetSpreadRebateRatesAsync(login);
        return Ok(rows);
    }

    /// <summary>PUT /api/equity-pnl-config/spread-rebates — bulk upsert.</summary>
    [HttpPut("spread-rebates")]
    public async Task<IActionResult> UpsertSpreadRebates([FromBody] List<SpreadRebateRate> rates)
    {
        if (rates == null || rates.Count == 0)
            return BadRequest(new { error = "at least one row required" });
        var n = await _supabase.UpsertSpreadRebateRatesAsync(rates);
        return Ok(new { upserted = n });
    }

    /// <summary>DELETE /api/equity-pnl-config/spread-rebates?login=&amp;source=&amp;symbol=</summary>
    [HttpDelete("spread-rebates")]
    public async Task<IActionResult> DeleteSpreadRebate(
        [FromQuery] long login,
        [FromQuery] string source,
        [FromQuery] string symbol)
    {
        if (login <= 0 || string.IsNullOrEmpty(source) || string.IsNullOrEmpty(symbol))
            return BadRequest(new { error = "login, source, symbol all required" });
        var ok = await _supabase.DeleteSpreadRebateRateAsync(login, source, symbol);
        return ok ? NoContent() : StatusCode(500, new { error = "delete failed" });
    }
}
