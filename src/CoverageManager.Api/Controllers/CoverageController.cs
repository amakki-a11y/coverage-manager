using Microsoft.AspNetCore.Mvc;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;
using CoverageManager.Api.Services;

namespace CoverageManager.Api.Controllers;

/// <summary>
/// Inbound sink for the Python collector AND browser-facing proxy for its
/// read endpoints. The collector binds to 127.0.0.1:8100 and is firewalled
/// from external traffic in production, so everything the dashboard needs
/// from it has to go through this controller.
///
/// <para>Routes:</para>
/// <list type="bullet">
///   <item><c>POST /api/coverage/positions</c> — collector → backend position push.</item>
///   <item><c>GET  /api/coverage/deals?from&amp;to</c> — browser → collector proxy.</item>
///   <item><c>GET  /api/coverage/health</c>           — browser → collector proxy.</item>
/// </list>
/// </summary>
[ApiController]
[Route("api/coverage")]
public class CoverageController : ControllerBase
{
    private readonly PositionManager _positionManager;
    private readonly ExposureBroadcastService _broadcastService;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<CoverageController> _logger;

    private const string CollectorUrl = "http://127.0.0.1:8100";

    public CoverageController(
        PositionManager positionManager,
        ExposureBroadcastService broadcastService,
        IHttpClientFactory httpFactory,
        ILogger<CoverageController> logger)
    {
        _positionManager = positionManager;
        _broadcastService = broadcastService;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/coverage/positions — full snapshot of open positions on the LP side.
    /// Body is the array the Python collector returns from <c>mt5.positions_get()</c>.
    /// The snapshot is treated as authoritative (replaces any prior coverage state).
    /// </summary>
    [HttpPost("positions")]
    public IActionResult UpdatePositions([FromBody] CoveragePositionDto[] positions)
    {
        _positionManager.UpdateCoveragePositions(positions);
        _broadcastService.MarkDirty();
        return Ok(new { received = positions.Length });
    }

    /// <summary>
    /// GET /api/coverage/deals — proxies <c>GET /deals?from=&amp;to=</c> on the Python
    /// collector. Dashboard uses this to populate the Coverage closed row on
    /// Exposure / P&amp;L tabs.
    /// </summary>
    [HttpGet("deals")]
    public async Task<IActionResult> GetDeals(
        [FromQuery] string from,
        [FromQuery] string to,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            return BadRequest(new { error = "from and to (YYYY-MM-DD) are required" });
        return await ProxyGetAsync($"/deals?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}", ct);
    }

    /// <summary>
    /// GET /api/coverage/health — proxies the collector's <c>/health</c> endpoint.
    /// Feeds the "Collector" dot in the top-bar health indicator.
    /// </summary>
    [HttpGet("health")]
    public Task<IActionResult> GetHealth(CancellationToken ct) =>
        ProxyGetAsync("/health", ct);

    private async Task<IActionResult> ProxyGetAsync(string path, CancellationToken ct)
    {
        try
        {
            var http = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(10);
            var res = await http.GetAsync($"{CollectorUrl}{path}", ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            return new ContentResult
            {
                Content = body,
                ContentType = "application/json",
                StatusCode = (int)res.StatusCode,
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Collector proxy failed for {Path}", path);
            return StatusCode(503, new { error = "Collector unreachable", detail = ex.Message });
        }
        catch (TaskCanceledException)
        {
            return StatusCode(504, new { error = "Collector timed out" });
        }
    }
}
