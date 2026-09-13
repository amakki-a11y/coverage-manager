using CoverageManager.Core.Models;

namespace CoverageManager.Core.Engines;

/// <summary>
/// The ONE mapping from a feed deal (<see cref="ClosedDeal"/>) to a stored row
/// (<see cref="DealRecord"/>). <c>DataSyncService</c> (the normal writer) and the feed/store
/// self-check both use it: if they built records differently, the self-check would "repair"
/// every row the writer produced -- canonical_symbol above all -- and the two would fight on
/// every run.
/// </summary>
public static class DealRecordMapper
{
    public static DealRecord FromClosedDeal(ClosedDeal d, PositionManager positionManager) => new()
    {
        DealId = (long)d.DealId,
        Source = "bbook",
        Login = (long)d.Login,
        Symbol = d.Symbol,
        CanonicalSymbol = ResolveCanonical(d.Symbol, positionManager),
        Direction = d.Direction,
        // Preserve the real MT5 DealAction code (0=BUY, 1=SELL, 2=BALANCE, 3=CREDIT, 4=CHARGE, 5=CORRECTION, ...):
        // balance/credit deals feed the Equity P&L tab and must not be re-classified as trades.
        Action = (int)d.Action,
        Entry = (int)d.Entry,
        Volume = d.VolumeLots,
        Price = d.Price,
        Profit = d.Profit,
        Commission = d.Commission,
        Swap = d.Swap,
        Fee = d.Fee,
        OrderId = d.OrderId == 0 ? null : (long?)d.OrderId,
        PositionId = d.PositionId == 0 ? null : (long?)d.PositionId,
        DealTime = d.Time
    };

    /// <summary>
    /// Stored row back to the in-memory deal shape, for Postgres-backed history. The inverse of
    /// <see cref="FromClosedDeal"/> for every field the P&amp;L rules read.
    /// </summary>
    public static ClosedDeal ToClosedDeal(DealRecord r) => new()
    {
        DealId = (ulong)r.DealId,
        Login = (ulong)r.Login,
        Symbol = r.Symbol,
        Direction = r.Direction,
        VolumeLots = r.Volume,
        Price = r.Price,
        Profit = r.Profit,
        Commission = r.Commission,
        Swap = r.Swap,
        Fee = r.Fee,
        Entry = (uint)r.Entry,
        Action = (uint)r.Action,
        OrderId = (ulong)(r.OrderId ?? 0),
        PositionId = (ulong)(r.PositionId ?? 0),
        Time = r.DealTime.Kind == DateTimeKind.Utc ? r.DealTime : DateTime.SpecifyKind(r.DealTime, DateTimeKind.Utc),
    };

    /// <summary>Mapped canonical name when a symbol mapping exists, else the raw symbol with a short
    /// trailing ".xx" suffix and trailing dashes removed, uppercased.</summary>
    public static string ResolveCanonical(string rawSymbol, PositionManager positionManager)
    {
        var m = positionManager.FindMapping(rawSymbol, "bbook");
        if (m != null) return m.CanonicalName.ToUpperInvariant();

        var s = (rawSymbol ?? string.Empty).Trim();
        var dot = s.LastIndexOf('.');
        if (dot >= 0 && s.Length - dot <= 3) s = s.Substring(0, dot);
        while (s.EndsWith("-")) s = s[..^1];
        return s.ToUpperInvariant();
    }
}
