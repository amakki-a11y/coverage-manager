using CoverageManager.Core.Models.Bridge;

namespace CoverageManager.Api.Services;

/// <summary>Connection state of the active Centroid feed — rendered as a color in the UI health dot.</summary>
public enum CentroidConnectionState
{
    Disconnected,
    Connecting,
    LoggedIn,
    Error,
    Stubbed,
}

/// <summary>
/// Snapshot of feed health returned by <see cref="ICentroidBridgeService.GetHealth"/>.
/// Surfaced by <c>GET /api/bridge/health</c> and fed into the top-right health dot.
/// </summary>
public class CentroidHealth
{
    public CentroidConnectionState State { get; set; }
    public string Mode { get; set; } = "Stub";
    public DateTime? LastMessageUtc { get; set; }
    public string? LastError { get; set; }
    public long MessagesReceived { get; set; }
}

/// <summary>
/// Abstraction over the Centroid Dropcopy FIX 4.4 feed. Today: a stub implementation that
/// emits synthetic BridgeDeals. Tomorrow: a QuickFIX/n session backed impl.
/// </summary>
public interface ICentroidBridgeService
{
    CentroidHealth GetHealth();

    /// <summary>
    /// Fetch historical deals from the feed's cached buffer (no DB).
    /// Used for backfilling the Bridge tab's initial load from the in-memory window.
    /// </summary>
    Task<IReadOnlyList<BridgeDeal>> GetDealsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? canonicalSymbol = null,
        CancellationToken ct = default);

    /// <summary>
    /// Subscribe to the live deal stream. Callback invoked on every normalized fill.
    /// Returns an IDisposable; disposing unsubscribes.
    /// </summary>
    IDisposable Subscribe(Action<BridgeDeal> onDeal);

    /// <summary>
    /// Look up client-side markup detail for a given Centroid order ID.
    /// Populated by the orders_report REST poller (Live feed only; Stub returns null).
    /// </summary>
    ClientOrderDetail? GetClientDetail(string cenOrdId);
}
