using CoverageManager.Core.Models.Bridge;

namespace CoverageManager.Api.Services;

/// <summary>
/// Thin ICentroidBridgeService facade that forwards every call into the live
/// <see cref="BridgeFeedHost"/>. Lets existing consumers (BridgeExecutionWorker,
/// BridgeController) keep depending on the interface while the host swaps the
/// underlying implementation at runtime.
/// </summary>
public class BridgeFeedHostAdapter : ICentroidBridgeService
{
    private readonly BridgeFeedHost _host;

    public BridgeFeedHostAdapter(BridgeFeedHost host) { _host = host; }

    public CentroidHealth GetHealth() => _host.GetHealth();

    public Task<IReadOnlyList<BridgeDeal>> GetDealsAsync(
        DateTime fromUtc, DateTime toUtc, string? canonicalSymbol = null, CancellationToken ct = default)
        => _host.GetDealsAsync(fromUtc, toUtc, canonicalSymbol, ct);

    public IDisposable Subscribe(Action<BridgeDeal> onDeal) => _host.Subscribe(onDeal);

    public ClientOrderDetail? GetClientDetail(string cenOrdId) => _host.GetClientDetail(cenOrdId);
}
