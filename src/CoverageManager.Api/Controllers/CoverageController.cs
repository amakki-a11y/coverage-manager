using Microsoft.AspNetCore.Mvc;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;
using CoverageManager.Api.Services;

namespace CoverageManager.Api.Controllers;

/// <summary>
/// Inbound sink for the Python collector. The collector polls the LP MT5 terminal
/// every 100ms and POSTs the full coverage-side position snapshot here. Updates
/// <c>PositionManager</c> and marks the broadcast service dirty so the next
/// WebSocket tick reflects the new coverage state.
/// </summary>
[ApiController]
[Route("api/coverage")]
public class CoverageController : ControllerBase
{
    private readonly PositionManager _positionManager;
    private readonly ExposureBroadcastService _broadcastService;

    public CoverageController(PositionManager positionManager, ExposureBroadcastService broadcastService)
    {
        _positionManager = positionManager;
        _broadcastService = broadcastService;
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
}
