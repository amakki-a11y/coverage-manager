namespace CoverageManager.Core.Models;

/// <summary>
/// Per-symbol breakdown returned by GET /api/exposure/pnl/period.
///
///   FloatingDelta = CurrentFloating − BeginFloating     (change in unrealized P&L during period)
///   Net           = FloatingDelta + Settled             (realized + unrealized change)
/// </summary>
public class PeriodPnLSide
{
    /// <summary>Floating P&L captured at the Begin snapshot. 0 when no snapshot existed before the period.</summary>
    public decimal BeginFloating { get; set; }

    /// <summary>Current floating P&L on live open positions.</summary>
    public decimal CurrentFloating { get; set; }

    /// <summary>CurrentFloating − BeginFloating. Positive = open book improved during period.</summary>
    public decimal FloatingDelta { get; set; }

    /// <summary>Realized P&L from deals closed within the period (profit + commission + swap + fee).</summary>
    public decimal Settled { get; set; }

    /// <summary>FloatingDelta + Settled.</summary>
    public decimal Net { get; set; }

    /// <summary>True when BeginFloating came from a real snapshot; false when treated as 0 (no history).</summary>
    public bool BeginFromSnapshot { get; set; }

    /// <summary>True when there is a live open position for this symbol; false means "no position" (distinguishes 0 floating from no position).</summary>
    public bool HasOpenPosition { get; set; }
}

public class PeriodPnLEdge
{
    /// <summary>Clients.FloatingDelta − Coverage.FloatingDelta.</summary>
    public decimal Floating { get; set; }

    /// <summary>Clients.Settled − Coverage.Settled.</summary>
    public decimal Settled { get; set; }

    /// <summary>Clients.Net − Coverage.Net.</summary>
    public decimal Net { get; set; }
}

public class PeriodPnLRow
{
    public string CanonicalSymbol { get; set; } = string.Empty;
    public PeriodPnLSide BBook { get; set; } = new();
    public PeriodPnLSide Coverage { get; set; } = new();
    public PeriodPnLEdge Edge { get; set; } = new();
}

public class PeriodPnLResponse
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public DateTime BeginAnchorUtc { get; set; }
    public List<PeriodPnLRow> Rows { get; set; } = new();
    public PeriodPnLRow Totals { get; set; } = new() { CanonicalSymbol = "TOTAL" };
}
