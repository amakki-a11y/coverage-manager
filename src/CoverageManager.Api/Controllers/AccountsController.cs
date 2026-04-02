using Microsoft.AspNetCore.Mvc;
using CoverageManager.Api.Services;

namespace CoverageManager.Api.Controllers;

[ApiController]
[Route("api/accounts")]
public class AccountsController : ControllerBase
{
    private readonly SupabaseService _supabase;

    public AccountsController(SupabaseService supabase)
    {
        _supabase = supabase;
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
}
