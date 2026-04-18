using System.Collections.Concurrent;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models.Bridge;

namespace CoverageManager.Api.Services;

/// <summary>
/// Thread-safe in-memory store of ExecutionPairs keyed by CLIENT dealId.
/// Incoming BridgeDeals are fed through <see cref="HandleDeal(BridgeDeal)"/>:
///   - CLIENT deals create a new pair (or refresh an existing one).
///   - COV_OUT deals are attributed to an open pair within the pairing window.
///
/// This is the "state machine" referenced in the Phase 2.5 spec. Persistence and broadcast
/// are handled by the owner (BridgeExecutionWorker) via the <see cref="PairUpdated"/> event.
/// </summary>
public class BridgeExecutionStore
{
    private readonly ConcurrentDictionary<string, ExecutionPair> _byClientDealId = new();
    private readonly ConcurrentDictionary<string, string> _orphanCovByCenOrdId = new();
    private readonly ConcurrentDictionary<string, List<BridgeDeal>> _pendingCovByCenOrdId = new();
    private readonly int _pairingWindowMs;
    private readonly ILogger<BridgeExecutionStore> _logger;

    public event Action<ExecutionPair>? PairUpdated;

    public BridgeExecutionStore(int pairingWindowMs, ILogger<BridgeExecutionStore> logger)
    {
        _pairingWindowMs = pairingWindowMs > 0 ? pairingWindowMs : BridgePairingEngine.DefaultPairingWindowMs;
        _logger = logger;
    }

    public int Count => _byClientDealId.Count;

    public IEnumerable<ExecutionPair> Snapshot() =>
        _byClientDealId.Values.OrderByDescending(p => p.ClientTimeUtc).ToList();

    public IEnumerable<ExecutionPair> Query(DateTime fromUtc, DateTime toUtc, string? canonicalSymbol, int limit)
    {
        return _byClientDealId.Values
            .Where(p => p.ClientTimeUtc >= fromUtc && p.ClientTimeUtc <= toUtc)
            .Where(p => canonicalSymbol == null || string.Equals(p.Symbol, canonicalSymbol, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.ClientTimeUtc)
            .Take(limit);
    }

    public void HandleDeal(BridgeDeal deal)
    {
        if (string.IsNullOrEmpty(deal.CanonicalSymbol))
            deal.CanonicalSymbol = (deal.Symbol ?? string.Empty).ToUpperInvariant();

        switch (deal.Source)
        {
            case BridgeSource.CLIENT:
            case BridgeSource.UNCLASSIFIED:
                OnClientFill(deal);
                break;
            case BridgeSource.COV_OUT:
                OnCoverageFill(deal);
                break;
        }
    }

    private void OnClientFill(BridgeDeal client)
    {
        var pair = new ExecutionPair
        {
            ClientDealId = client.DealId,
            CenOrdId = client.CenOrdId,
            Symbol = client.CanonicalSymbol ?? client.Symbol,
            Side = client.Side,
            ClientVolume = client.Volume,
            ClientPrice = client.Price,
            ClientTimeUtc = client.TimeUtc,
            ClientMtTicket = client.MtTicket,
            ClientMtDealId = client.MtDealId,
            ClientMtLogin = client.MtLogin,
            CovFills = new List<CovFill>(),
        };

        // Pull any pending cov fills keyed by this CenOrdId (cov arrived before client).
        if (!string.IsNullOrEmpty(client.CenOrdId) &&
            _pendingCovByCenOrdId.TryRemove(client.CenOrdId, out var pendings))
        {
            foreach (var p in pendings)
                TryAttributeCoverage(pair, p);
        }

        BridgePairingEngine.ComputeMetrics(pair);
        _byClientDealId[pair.ClientDealId] = pair;
        RaiseUpdated(pair);
    }

    private void OnCoverageFill(BridgeDeal cov)
    {
        // Prefer matching by CenOrdId when it's populated and unique.
        if (!string.IsNullOrEmpty(cov.CenOrdId))
        {
            var byCen = _byClientDealId.Values
                .Where(p => p.CenOrdId == cov.CenOrdId)
                .OrderBy(p => Math.Abs((cov.TimeUtc - p.ClientTimeUtc).TotalMilliseconds))
                .FirstOrDefault();
            if (byCen != null && TryAttributeCoverage(byCen, cov))
            {
                BridgePairingEngine.ComputeMetrics(byCen);
                RaiseUpdated(byCen);
                return;
            }
        }

        // Fall back to (symbol, side, time window). Closest-in-time pair wins.
        var candidate = _byClientDealId.Values
            .Where(p =>
                p.Side == cov.Side &&
                string.Equals(p.Symbol, cov.CanonicalSymbol, StringComparison.OrdinalIgnoreCase) &&
                Math.Abs((cov.TimeUtc - p.ClientTimeUtc).TotalMilliseconds) <= _pairingWindowMs)
            .OrderBy(p => Math.Abs((cov.TimeUtc - p.ClientTimeUtc).TotalMilliseconds))
            .FirstOrDefault();

        if (candidate != null && TryAttributeCoverage(candidate, cov))
        {
            BridgePairingEngine.ComputeMetrics(candidate);
            RaiseUpdated(candidate);
            return;
        }

        // No matching client yet — buffer by CenOrdId so we can attach it when the client arrives.
        if (!string.IsNullOrEmpty(cov.CenOrdId))
        {
            _pendingCovByCenOrdId.AddOrUpdate(
                cov.CenOrdId,
                _ => new List<BridgeDeal> { cov },
                (_, existing) => { lock (existing) { existing.Add(cov); } return existing; });
            return;
        }

        _orphanCovByCenOrdId[cov.DealId] = cov.DealId;
    }

    private bool TryAttributeCoverage(ExecutionPair pair, BridgeDeal cov)
    {
        // Don't double-attribute
        if (pair.CovFills.Any(f => f.DealId == cov.DealId)) return false;

        var diff = (int)Math.Round((cov.TimeUtc - pair.ClientTimeUtc).TotalMilliseconds);
        if (Math.Abs(diff) > _pairingWindowMs) return false;

        pair.CovFills.Add(new CovFill
        {
            DealId = cov.DealId,
            Volume = cov.Volume,
            Price = cov.Price,
            TimeUtc = cov.TimeUtc,
            TimeDiffMs = diff,
            LpName = cov.LpName,
            MtTicket = cov.MtTicket,
            MtDealId = cov.MtDealId,
            MakerOrderId = cov.MakerOrderId,
            RawPrice = cov.RawPrice,
            ExtMarkup = cov.ExternalMarkup,
        });
        // Keep insertion order by |diff| for stable UI rendering.
        pair.CovFills.Sort((a, b) => Math.Abs(a.TimeDiffMs).CompareTo(Math.Abs(b.TimeDiffMs)));
        return true;
    }

    private void RaiseUpdated(ExecutionPair pair)
    {
        var handler = PairUpdated;
        if (handler == null) return;
        try { handler(pair); }
        catch (Exception ex) { _logger.LogWarning(ex, "PairUpdated subscriber threw"); }
    }

    /// <summary>
    /// Evict pairs older than the provided cutoff. Called periodically by the worker.
    /// </summary>
    public int EvictOlderThan(DateTime cutoffUtc)
    {
        var stale = _byClientDealId
            .Where(kvp => kvp.Value.ClientTimeUtc < cutoffUtc)
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var k in stale) _byClientDealId.TryRemove(k, out _);
        return stale.Count;
    }
}
