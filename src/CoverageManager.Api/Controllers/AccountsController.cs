using Microsoft.AspNetCore.Mvc;
using CoverageManager.Api.Services;
using CoverageManager.Connector;

namespace CoverageManager.Api.Controllers;

[ApiController]
[Route("api/accounts")]
public class AccountsController : ControllerBase
{
    private readonly SupabaseService _supabase;
    private readonly MT5ManagerConnection _mt5Connection;

    public AccountsController(SupabaseService supabase, MT5ManagerConnection mt5Connection)
    {
        _supabase = supabase;
        _mt5Connection = mt5Connection;
    }

    [HttpGet]
    public async Task<IActionResult> GetAccounts([FromQuery] string? source = null)
    {
        var accounts = await _supabase.GetTradingAccountsAsync(source);
        return Ok(new { count = accounts.Count, accounts });
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] string? from = null,
        [FromQuery] string? symbol = null,
        [FromQuery] long? login = null)
    {
        DateTime? fromDate = from != null ? DateTime.Parse(from) : DateTime.UtcNow.Date;
        var entries = await _supabase.GetAuditLogAsync(fromDate, symbol, login);
        return Ok(new { count = entries.Count, entries });
    }

    /// <summary>
    /// POST /api/accounts/backfill-deals?from=2026-03-01&to=2026-03-31
    /// Backfills historical deals from MT5 and persists to Supabase.
    /// </summary>
    [HttpPost("backfill-deals")]
    public async Task<IActionResult> BackfillDeals([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        if (!_mt5Connection.IsConnected)
            return StatusCode(503, "MT5 not connected");

        var fromOffset = new DateTimeOffset(from.Date, TimeSpan.Zero);
        var toOffset = new DateTimeOffset(to.Date.AddDays(1), TimeSpan.Zero);

        var count = _mt5Connection.ReloadDeals(fromOffset, toOffset);
        if (count < 0)
            return StatusCode(503, "MT5 not ready");

        return Ok(new { message = $"Backfilled {count} deals from {from:yyyy-MM-dd} to {to:yyyy-MM-dd}. DataSyncService will persist to Supabase within 30s." });
    }

    /// <summary>
    /// GET /api/accounts/deals?source=bbook&from=2026-03-01&to=2026-03-31
    /// Query historical deals from Supabase.
    /// </summary>
    [HttpGet("deals")]
    public async Task<IActionResult> GetDeals(
        [FromQuery] string source = "bbook",
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var fromDate = from ?? DateTime.UtcNow.Date;
        var toDate = to ?? DateTime.UtcNow.Date.AddDays(1);
        var deals = await _supabase.GetDealsAsync(source, fromDate, toDate);
        return Ok(new { count = deals.Count, deals });
    }
}
