using Microsoft.AspNetCore.Mvc;
using CoverageManager.Core.Models;
using CoverageManager.Api.Services;

namespace CoverageManager.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly SupabaseService _supabase;

    public SettingsController(SupabaseService supabase)
    {
        _supabase = supabase;
    }

    /// <summary>
    /// GET /api/settings/accounts — list all account settings
    /// </summary>
    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts()
    {
        var accounts = await _supabase.GetAccountSettingsAsync();
        return Ok(accounts);
    }

    /// <summary>
    /// POST /api/settings/accounts — add or update an account
    /// </summary>
    [HttpPost("accounts")]
    public async Task<IActionResult> UpsertAccount([FromBody] AccountSettings settings)
    {
        var result = await _supabase.UpsertAccountSettingsAsync(settings);
        if (result == null) return StatusCode(500, "Failed to save account settings");
        return Ok(result);
    }

    /// <summary>
    /// PUT /api/settings/accounts/{id} — update an account
    /// </summary>
    [HttpPut("accounts/{id:guid}")]
    public async Task<IActionResult> UpdateAccount(Guid id, [FromBody] AccountSettings settings)
    {
        settings.Id = id;
        var result = await _supabase.UpsertAccountSettingsAsync(settings);
        if (result == null) return StatusCode(500, "Failed to update account settings");
        return Ok(result);
    }

    /// <summary>
    /// DELETE /api/settings/accounts/{id} — delete an account
    /// </summary>
    [HttpDelete("accounts/{id:guid}")]
    public async Task<IActionResult> DeleteAccount(Guid id)
    {
        var success = await _supabase.DeleteAccountSettingsAsync(id);
        if (!success) return StatusCode(500, "Failed to delete account settings");
        return NoContent();
    }
}
