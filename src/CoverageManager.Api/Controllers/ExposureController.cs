using Microsoft.AspNetCore.Mvc;
using CoverageManager.Core.Engines;
using CoverageManager.Connector;

namespace CoverageManager.Api.Controllers;

[ApiController]
[Route("api/exposure")]
public class ExposureController : ControllerBase
{
    private readonly ExposureEngine _exposureEngine;
    private readonly PositionManager _positionManager;
    private readonly MT5ManagerConnection _mt5Connection;
    private readonly DealStore _dealStore;

    public ExposureController(
        ExposureEngine exposureEngine,
        PositionManager positionManager,
        MT5ManagerConnection mt5Connection,
        DealStore dealStore)
    {
        _exposureEngine = exposureEngine;
        _positionManager = positionManager;
        _mt5Connection = mt5Connection;
        _dealStore = dealStore;
    }

    /// <summary>
    /// GET /api/exposure/summary — current exposure snapshot
    /// </summary>
    [HttpGet("summary")]
    public IActionResult GetSummary()
    {
        var exposure = _exposureEngine.CalculateExposure();
        return Ok(exposure);
    }

    /// <summary>
    /// GET /api/exposure/positions — all open positions
    /// </summary>
    [HttpGet("positions")]
    public IActionResult GetPositions()
    {
        var positions = _positionManager.GetAllPositions();
        return Ok(positions);
    }

    /// <summary>
    /// GET /api/exposure/status — MT5 connection status
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            mt5Connected = _mt5Connection.IsConnected,
            mt5Server = _mt5Connection.ConnectedServer,
            bbookPositions = _mt5Connection.PositionCount,
            loginCount = _mt5Connection.LoginCount
        });
    }

    /// <summary>
    /// GET /api/exposure/pnl — realized P&L with daily breakdown (from Mar 29)
    /// </summary>
    [HttpGet("pnl")]
    public IActionResult GetPnL()
    {
        var pnl = _dealStore.GetPnLBySymbol();
        var daily = _dealStore.GetPnLByDay();
        return Ok(new
        {
            totalDeals = _dealStore.DealCount,
            symbols = pnl,
            daily
        });
    }

    /// <summary>
    /// GET /api/exposure/deals — all closed deals
    /// </summary>
    [HttpGet("deals")]
    public IActionResult GetDeals()
    {
        var deals = _dealStore.GetAllDeals();
        return Ok(deals);
    }

    /// <summary>
    /// POST /api/exposure/pnl/reload?from=2026-03-01&to=2026-03-31 — reload deals for date range
    /// </summary>
    [HttpPost("pnl/reload")]
    public IActionResult ReloadPnL([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        if (!_mt5Connection.IsConnected)
            return StatusCode(503, "MT5 not connected");

        var fromOffset = new DateTimeOffset(from.Date, TimeSpan.Zero);
        var toOffset = new DateTimeOffset(to.Date.AddDays(1), TimeSpan.Zero); // Include full end day

        var count = _mt5Connection.ReloadDeals(fromOffset, toOffset);
        if (count < 0)
            return StatusCode(503, "MT5 not ready");

        var pnl = _dealStore.GetPnLBySymbol();
        var daily = _dealStore.GetPnLByDay();
        return Ok(new
        {
            totalDeals = _dealStore.DealCount,
            symbols = pnl,
            daily
        });
    }
}
