using Microsoft.AspNetCore.Mvc;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;
using CoverageManager.Api.Services;

namespace CoverageManager.Api.Controllers;

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
    /// Receives positions from Python collector.
    /// POST /api/coverage/positions
    /// </summary>
    [HttpPost("positions")]
    public IActionResult UpdatePositions([FromBody] CoveragePositionDto[] positions)
    {
        _positionManager.UpdateCoveragePositions(positions);
        _broadcastService.MarkDirty();
        return Ok(new { received = positions.Length });
    }
}
