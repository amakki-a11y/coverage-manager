using Microsoft.AspNetCore.Mvc;
using CoverageManager.Core.Models;
using CoverageManager.Core.Models.Bridge;
using CoverageManager.Api.Services;

namespace CoverageManager.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly SupabaseService _supabase;
    private readonly BridgeFeedHost _bridgeHost;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        SupabaseService supabase,
        BridgeFeedHost bridgeHost,
        ILogger<SettingsController> logger)
    {
        _supabase = supabase;
        _bridgeHost = bridgeHost;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/settings/accounts — list all account settings.
    /// Password is never echoed; frontend sees `passwordSet: true|false` instead.
    /// </summary>
    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts()
    {
        var accounts = await _supabase.GetAccountSettingsAsync();
        return Ok(accounts.Select(RedactAccount));
    }

    /// <summary>
    /// POST /api/settings/accounts — add or update an account.
    /// Response is redacted (no password echoed back).
    /// </summary>
    [HttpPost("accounts")]
    public async Task<IActionResult> CreateAccount([FromBody] AccountSettings settings)
    {
        var result = await _supabase.CreateAccountSettingsAsync(settings);
        if (result == null) return StatusCode(500, "Failed to save account settings");
        return Ok(RedactAccount(result));
    }

    /// <summary>
    /// PUT /api/settings/accounts/{id} — update an account.
    /// Response is redacted (no password echoed back).
    /// </summary>
    [HttpPut("accounts/{id:guid}")]
    public async Task<IActionResult> UpdateAccount(Guid id, [FromBody] AccountSettings settings)
    {
        var result = await _supabase.UpdateAccountSettingsAsync(id, settings);
        if (result == null) return StatusCode(500, "Failed to update account settings");
        return Ok(RedactAccount(result));
    }

    /// <summary>
    /// Redact the password field from an AccountSettings before returning it over
    /// the wire. Preserves the original snake_case JSON shape the UI depends on
    /// (account_type, group_mask, is_active, …) but blanks the password so it
    /// never leaves the server. Mutates in place — callers already got fresh
    /// instances from SupabaseService / the POST body.
    /// </summary>
    private static AccountSettings RedactAccount(AccountSettings a)
    {
        a.Password = string.Empty;
        return a;
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

    /// <summary>
    /// GET /api/settings/moved-accounts — list moved/excluded accounts
    /// </summary>
    [HttpGet("moved-accounts")]
    public async Task<IActionResult> GetMovedAccounts()
    {
        var accounts = await _supabase.GetMovedAccountsAsync();
        return Ok(accounts);
    }

    // ── Bridge (Centroid CS 360 REST + WebSocket) settings ──

    /// <summary>
    /// GET /api/settings/bridge — current Bridge settings. Password is never returned.
    /// </summary>
    [HttpGet("bridge")]
    public async Task<IActionResult> GetBridge()
    {
        try
        {
            var s = await _supabase.GetBridgeSettingsAsync() ?? new BridgeSettings();
            return Ok(new
            {
                id = s.Id,
                enabled = s.Enabled,
                mode = s.Mode,
                baseUrl = s.BaseUrl,
                clientCode = s.ClientCode,
                username = s.Username,
                passwordSet = !string.IsNullOrEmpty(s.Password),
                notes = s.Notes,
                updatedAt = s.UpdatedAt,
                activeMode = _bridgeHost.CurrentMode,
                feedHealth = _bridgeHost.GetHealth(),
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET /api/settings/bridge failed");
            return StatusCode(500, new { error = "Internal error" });
        }
    }

    public sealed class UpdateBridgeRequest
    {
        public bool Enabled { get; set; }
        public string Mode { get; set; } = "Stub";
        public string BaseUrl { get; set; } = "https://bridge.centroidsol.com";
        public string ClientCode { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Only update the password when the client sends a non-null value.
        /// Empty string => wipe; null => leave the existing password untouched.
        /// </summary>
        public string? Password { get; set; }

        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>
    /// PUT /api/settings/bridge — upsert the singleton row + reload the feed.
    /// </summary>
    [HttpPut("bridge")]
    public async Task<IActionResult> UpdateBridge([FromBody] UpdateBridgeRequest body)
    {
        try
        {
            var existing = await _supabase.GetBridgeSettingsAsync() ?? new BridgeSettings();
            existing.Enabled = body.Enabled;
            existing.Mode = string.IsNullOrWhiteSpace(body.Mode) ? "Stub" : body.Mode;
            existing.BaseUrl = string.IsNullOrWhiteSpace(body.BaseUrl) ? "https://bridge.centroidsol.com" : body.BaseUrl.TrimEnd('/');
            existing.ClientCode = body.ClientCode ?? string.Empty;
            existing.Username = body.Username ?? string.Empty;
            if (body.Password != null) existing.Password = body.Password;
            existing.Notes = body.Notes ?? string.Empty;

            var saved = await _supabase.UpsertBridgeSettingsAsync(existing);
            if (saved == null) return StatusCode(500, new { error = "Failed to save" });

            var targetMode = saved.Enabled && saved.IsLoginReady() ? "Live" : "Stub";
            try
            {
                await _bridgeHost.SwitchAsync(targetMode);
            }
            catch (Exception swEx)
            {
                _logger.LogError(swEx, "Bridge switch to {Mode} failed after settings save", targetMode);
                await _bridgeHost.SwitchAsync("Stub");
                return Ok(new { saved = true, activeMode = "Stub", error = swEx.Message });
            }

            return Ok(new { saved = true, activeMode = _bridgeHost.CurrentMode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PUT /api/settings/bridge failed");
            return StatusCode(500, new { error = "Internal error" });
        }
    }

    /// <summary>
    /// POST /api/settings/bridge/reload — tear down and re-init the current feed.
    /// </summary>
    [HttpPost("bridge/reload")]
    public async Task<IActionResult> ReloadBridge()
    {
        try
        {
            var s = await _supabase.GetBridgeSettingsAsync();
            var mode = s?.Enabled == true && s.IsLoginReady() ? "Live" : "Stub";
            await _bridgeHost.SwitchAsync(mode);
            return Ok(new { activeMode = _bridgeHost.CurrentMode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST /api/settings/bridge/reload failed");
            return StatusCode(500, new { error = "Internal error" });
        }
    }

    public sealed class TestBridgeRequest
    {
        public string BaseUrl { get; set; } = "https://bridge.centroidsol.com";
        public string ClientCode { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        /// <summary>Optional — if null we use the persisted password for the test.</summary>
        public string? Password { get; set; }
    }

    /// <summary>
    /// POST /api/settings/bridge/test — validate creds by hitting POST /v2/api/login without
    /// persisting anything. Returns { success, status, error, elapsedMs }.
    /// The password field is optional: null => use the currently persisted password.
    /// </summary>
    [HttpPost("bridge/test")]
    public async Task<IActionResult> TestBridge([FromServices] IHttpClientFactory httpFactory, [FromBody] TestBridgeRequest body)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var baseUrl = string.IsNullOrWhiteSpace(body.BaseUrl) ? "https://bridge.centroidsol.com" : body.BaseUrl.TrimEnd('/');
            var username = body.Username?.Trim() ?? string.Empty;
            var password = body.Password;
            if (password == null)
            {
                // Use the stored password.
                var existing = await _supabase.GetBridgeSettingsAsync();
                password = existing?.Password ?? string.Empty;
            }

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return Ok(new { success = false, status = 0, error = "Username and password required", elapsedMs = sw.ElapsedMilliseconds });

            var http = httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(15);
            var (ok, _, error, status) = await RestCentroidBridgeService.LoginOnceAsync(http, baseUrl, username, password);
            sw.Stop();
            return Ok(new
            {
                success = ok,
                status,
                error,
                elapsedMs = sw.ElapsedMilliseconds,
                hint = ok
                    ? "Login OK. You can now enable Live mode."
                    : error != null && error.Contains("USER_NOT_FOUND", StringComparison.OrdinalIgnoreCase)
                        ? "Username not recognized by Centroid. It may differ from your UI login — your Centroid admin can confirm the exact API username."
                    : error != null && error.Contains("INVALID_CREDENTIALS", StringComparison.OrdinalIgnoreCase)
                        ? "Password rejected by Centroid. Double-check the password — it's the same one you use to log into the Centroid admin UI."
                    : status == 401
                        ? "Unauthorized. Check the password and that access_type is 'api' or 'both' (not 'ui' only)."
                    : status == 0
                        ? "Network error — base URL unreachable or TLS problem. Check the Base URL field."
                    : $"HTTP {status}. Full error above."
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "POST /api/settings/bridge/test failed");
            return Ok(new { success = false, status = 0, error = ex.Message, elapsedMs = sw.ElapsedMilliseconds });
        }
    }
}
