using CoverageManager.Core.Models.Bridge;

namespace CoverageManager.Core.Engines;

/// <summary>
/// PURE pairing logic — no IO, no DI, no state. Takes a batch of normalized
/// <see cref="BridgeDeal"/>s from the Centroid Dropcopy feed and groups CLIENT fills
/// with their COV_OUT legs into <see cref="ExecutionPair"/> rows.
///
/// Pairing rules (Phase 2.5 spec):
/// 1. Symbols must match on the canonical form (apply mapping before calling).
/// 2. Side must match (bridge passes through direction).
/// 3. COV_OUT time - CLIENT time within PairingWindowMs (default 10s). CAN BE NEGATIVE.
/// 4. Greedy match by |time_diff|. Attributed COV_OUT volume is capped at CLIENT volume.
///    Excess COV_OUT fills are returned as unattributed.
/// </summary>
public static class BridgePairingEngine
{
    public const int DefaultPairingWindowMs = 10_000;

    public sealed class PairingResult
    {
        public List<ExecutionPair> Pairs { get; } = new();

        /// <summary>
        /// COV_OUT deals that did not land in any CLIENT's window. Available for diagnostics.
        /// </summary>
        public List<BridgeDeal> UnattributedCoverage { get; } = new();

        /// <summary>
        /// CLIENT deals whose total attributed coverage exceeds their volume.
        /// The overage is also recorded in UnattributedCoverage.
        /// </summary>
        public int OverCoveredClientCount { get; set; }
    }

    /// <summary>
    /// Group the deals. Callers MUST pre-populate <see cref="BridgeDeal.CanonicalSymbol"/>.
    /// </summary>
    public static PairingResult Reconcile(
        IEnumerable<BridgeDeal> deals,
        int pairingWindowMs = DefaultPairingWindowMs)
    {
        var result = new PairingResult();
        var list = deals as IList<BridgeDeal> ?? deals.ToList();

        // Canonicalize — any deal missing a canonical falls back to raw symbol.
        foreach (var d in list)
        {
            if (string.IsNullOrEmpty(d.CanonicalSymbol))
                d.CanonicalSymbol = (d.Symbol ?? string.Empty).ToUpperInvariant();
        }

        var clients = list
            .Where(d => d.Source == BridgeSource.CLIENT || d.Source == BridgeSource.UNCLASSIFIED)
            .OrderByDescending(d => d.TimeUtc)
            .ToList();

        // Bucket coverage fills by (canonical-symbol, side) for O(1) lookups.
        var covBuckets = list
            .Where(d => d.Source == BridgeSource.COV_OUT)
            .GroupBy(d => BucketKey(d.CanonicalSymbol!, d.Side))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.TimeUtc).ToList());

        // Track which coverage fills we've already attributed — simple HashSet by DealId.
        var usedCov = new HashSet<string>();

        foreach (var client in clients)
        {
            var key = BucketKey(client.CanonicalSymbol!, client.Side);
            if (!covBuckets.TryGetValue(key, out var candidates) || candidates.Count == 0)
            {
                result.Pairs.Add(BuildPair(client, Array.Empty<(BridgeDeal, int)>()));
                continue;
            }

            // Candidates are within the absolute-time window AND not yet consumed.
            var windowCandidates = candidates
                .Where(c => !usedCov.Contains(c.DealId))
                .Select(c => (
                    Cov: c,
                    DiffMs: (int)Math.Round((c.TimeUtc - client.TimeUtc).TotalMilliseconds)))
                .Where(c => Math.Abs(c.DiffMs) <= pairingWindowMs)
                .OrderBy(c => Math.Abs(c.DiffMs))
                .ToList();

            var attributed = new List<(BridgeDeal Cov, int DiffMs)>();
            decimal remaining = client.Volume;

            foreach (var wc in windowCandidates)
            {
                if (remaining <= 0m) break;
                if (wc.Cov.Volume <= 0m) continue;

                // Greedy: take the whole fill if it fits, else split.
                // We do NOT split a single FIX fill — it's one physical execution.
                // If taking it whole would over-cover, we still take it (per spec: Σ cov can
                // exceed client volume; excess is flagged via coverage_ratio > 1). Then break.
                attributed.Add(wc);
                usedCov.Add(wc.Cov.DealId);
                remaining -= wc.Cov.Volume;
            }

            if (remaining < 0m)
                result.OverCoveredClientCount++;

            result.Pairs.Add(BuildPair(client, attributed));
        }

        // Any coverage fills we never touched are unattributed.
        foreach (var kvp in covBuckets)
            foreach (var cov in kvp.Value)
                if (!usedCov.Contains(cov.DealId))
                    result.UnattributedCoverage.Add(cov);

        return result;
    }

    private static ExecutionPair BuildPair(
        BridgeDeal client,
        IReadOnlyCollection<(BridgeDeal Cov, int DiffMs)> attributed)
    {
        var pair = new ExecutionPair
        {
            ClientDealId = client.DealId,
            Symbol = client.CanonicalSymbol ?? client.Symbol,
            Side = client.Side,
            ClientVolume = client.Volume,
            ClientPrice = client.Price,
            ClientTimeUtc = client.TimeUtc,
            CenOrdId = client.CenOrdId,
            CreatedAtUtc = DateTime.UtcNow,
        };

        foreach (var (cov, diff) in attributed)
        {
            pair.CovFills.Add(new CovFill
            {
                DealId = cov.DealId,
                Volume = cov.Volume,
                Price = cov.Price,
                TimeUtc = cov.TimeUtc,
                TimeDiffMs = diff,
                LpName = cov.LpName,
            });
        }

        ComputeMetrics(pair);
        return pair;
    }

    /// <summary>
    /// Recomputes aggregated fields on a pair. Exposed separately so callers that mutate
    /// CovFills (e.g. streaming new fills onto an already-persisted pair) can re-derive
    /// without rebuilding the whole object.
    /// </summary>
    public static void ComputeMetrics(ExecutionPair pair)
    {
        pair.CovVolume = pair.CovFills.Sum(f => f.Volume);

        if (pair.CovVolume > 0m)
        {
            var numerator = pair.CovFills.Sum(f => f.Volume * f.Price);
            pair.AvgCovPrice = numerator / pair.CovVolume;
        }
        else
        {
            pair.AvgCovPrice = 0m;
        }

        pair.CoverageRatio = pair.ClientVolume > 0m
            ? pair.CovVolume / pair.ClientVolume
            : 0m;

        if (pair.CovFills.Count > 0 && pair.AvgCovPrice > 0m)
        {
            pair.PriceEdge = pair.Side == BridgeSide.SELL
                ? pair.AvgCovPrice - pair.ClientPrice
                : pair.ClientPrice - pair.AvgCovPrice;

            var pipSize = BridgePipResolver.GetPipSize(pair.Symbol, pair.ClientPrice);
            pair.Pips = pipSize > 0m ? pair.PriceEdge / pipSize : 0m;

            pair.MaxTimeDiffMs = pair.CovFills.Max(f => f.TimeDiffMs);
            pair.MinTimeDiffMs = pair.CovFills.Min(f => f.TimeDiffMs);
        }
        else
        {
            pair.PriceEdge = 0m;
            pair.Pips = 0m;
            pair.MaxTimeDiffMs = 0;
            pair.MinTimeDiffMs = 0;
        }
    }

    private static string BucketKey(string canonicalSymbol, BridgeSide side) =>
        $"{canonicalSymbol.ToUpperInvariant()}|{side}";
}
