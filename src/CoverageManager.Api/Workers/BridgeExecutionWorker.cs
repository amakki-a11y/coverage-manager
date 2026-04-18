using System.Threading.Channels;
using CoverageManager.Api.Services;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models.Bridge;

namespace CoverageManager.Api.Workers;

/// <summary>
/// Glue between the Centroid Dropcopy feed, the pairing store, Supabase persistence,
/// and the WebSocket broadcast hub.
///
/// Runs as an IHostedService:
///   1. Subscribes to ICentroidBridgeService deal stream.
///   2. Applies symbol mapping (raw → canonical) using PositionManager.
///   3. Applies CLIENT/COV_OUT classification from MtGroup using the regex rules below.
///   4. Feeds deals into BridgeExecutionStore.
///   5. When the store raises PairUpdated, upserts to Supabase (throttled) and broadcasts
///      on /ws/bridge.
/// </summary>
public class BridgeExecutionWorker : BackgroundService
{
    private readonly ICentroidBridgeService _feed;
    private readonly BridgeExecutionStore _store;
    private readonly BridgeSupabaseWriter _writer;
    private readonly BridgeBroadcastService _broadcast;
    private readonly CoverageManager.Core.Engines.PositionManager _positionManager;
    private readonly DealStore _dealStore;
    private readonly CoverageDealIndex _coverageIndex;
    private readonly IConfiguration _config;
    private readonly ILogger<BridgeExecutionWorker> _logger;

    // Group-name classification regex (configurable via Centroid:GroupClassification)
    private readonly System.Text.RegularExpressions.Regex _abookRegex;
    private readonly System.Text.RegularExpressions.Regex _bbookRegex;

    private readonly Channel<ExecutionPair> _persistQueue =
        Channel.CreateUnbounded<ExecutionPair>(new UnboundedChannelOptions { SingleReader = true });

    private IDisposable? _subscription;

    public BridgeExecutionWorker(
        ICentroidBridgeService feed,
        BridgeExecutionStore store,
        BridgeSupabaseWriter writer,
        BridgeBroadcastService broadcast,
        CoverageManager.Core.Engines.PositionManager positionManager,
        DealStore dealStore,
        CoverageDealIndex coverageIndex,
        IConfiguration config,
        ILogger<BridgeExecutionWorker> logger)
    {
        _feed = feed;
        _store = store;
        _writer = writer;
        _broadcast = broadcast;
        _positionManager = positionManager;
        _dealStore = dealStore;
        _coverageIndex = coverageIndex;
        _config = config;
        _logger = logger;

        var abookPattern = config["Centroid:GroupClassification:ABookPattern"]
                           ?? @"(^|\\)a-book($|\\)";
        var bbookPattern = config["Centroid:GroupClassification:BBookPattern"]
                           ?? @"(^|\\)b-book($|\\)";
        _abookRegex = new System.Text.RegularExpressions.Regex(abookPattern,
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        _bbookRegex = new System.Text.RegularExpressions.Regex(bbookPattern,
            System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BridgeExecutionWorker starting (mode: {Mode})", _feed.GetHealth().Mode);

        _store.PairUpdated += OnPairUpdated;
        _subscription = _feed.Subscribe(OnDealReceived);

        // Persistence loop — reads from the channel and upserts each pair to Supabase.
        _ = Task.Run(async () =>
        {
            await foreach (var pair in _persistQueue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await _writer.UpsertAsync(pair, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Persist loop error for pair {DealId}", pair.ClientDealId);
                }
            }
        }, stoppingToken);

        // Eviction + health tick every minute
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                var removed = _store.EvictOlderThan(DateTime.UtcNow.AddHours(-24));
                if (removed > 0)
                    _logger.LogDebug("Evicted {Removed} stale Bridge pairs", removed);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bridge worker tick failed");
            }
        }

        _subscription?.Dispose();
        _store.PairUpdated -= OnPairUpdated;
        _persistQueue.Writer.TryComplete();
        _logger.LogInformation("BridgeExecutionWorker stopped");
    }

    private void OnDealReceived(BridgeDeal deal)
    {
        try
        {
            Normalize(deal);
            _store.HandleDeal(deal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle incoming BridgeDeal {DealId}", deal.DealId);
        }
    }

    private void Normalize(BridgeDeal deal)
    {
        // 1. Canonicalize symbol through the existing B-Book ↔ LP mapping.
        if (string.IsNullOrEmpty(deal.CanonicalSymbol))
        {
            var mapping = _positionManager.FindMapping(deal.Symbol ?? string.Empty, source: "bridge");
            deal.CanonicalSymbol = mapping != null ? mapping.CanonicalName : (deal.Symbol ?? string.Empty).ToUpperInvariant();
        }

        // 2. Classify CLIENT/COV_OUT from MtGroup ONLY when the feed left it UNCLASSIFIED.
        //    Feeds that already know which side a deal is on (e.g. the maker_orders poller
        //    sets COV_OUT directly) must not be overridden here.
        if (deal.Source == BridgeSource.UNCLASSIFIED && !string.IsNullOrEmpty(deal.MtGroup))
        {
            if (_abookRegex.IsMatch(deal.MtGroup)) deal.Source = BridgeSource.COV_OUT;
            else if (_bbookRegex.IsMatch(deal.MtGroup)) deal.Source = BridgeSource.CLIENT;
        }
    }

    private void OnPairUpdated(ExecutionPair pair)
    {
        // Resolve Centroid ext_order -> real B-Book MT5 deal number (when we have the deal locally).
        // Safe to call repeatedly: only fills the value when currently null.
        if (!pair.ClientMtDealId.HasValue &&
            pair.ClientMtLogin.HasValue && pair.ClientMtTicket.HasValue)
        {
            pair.ClientMtDealId = _dealStore.GetDealIdByOrder(
                pair.ClientMtLogin.Value, pair.ClientMtTicket.Value);
        }

        // Resolve maker_order_id -> coverage MT5 deal ticket on the 96900 account.
        // FXGROW writes maker_order_id into the external_id field of each MT5 deal.
        foreach (var c in pair.CovFills)
        {
            if (c.MtDealId.HasValue) continue;
            if (!ulong.TryParse(c.MakerOrderId, out var makerId) || makerId == 0) continue;
            c.MtDealId = _coverageIndex.GetDealIdByMakerOrderId(makerId);
        }

        // Enrich with client-side markup detail from orders_report (keyed by cen_ord_id).
        if (!string.IsNullOrEmpty(pair.CenOrdId))
        {
            var d = _feed.GetClientDetail(pair.CenOrdId);
            if (d != null)
            {
                pair.ClientReqPrice    ??= d.ReqAvgPrice;
                pair.ClientTotalMarkup ??= d.TotalMarkup;
                pair.ClientExtMarkup   ??= d.ExtMarkup;
                pair.ClientAFillVolume ??= d.AFillVolume;
                pair.ClientBFillVolume ??= d.BFillVolume;
                pair.ClientAAvgPrice   ??= d.AAvgPrice;
                pair.ClientBAvgPrice   ??= d.BAvgPrice;
            }
        }

        _persistQueue.Writer.TryWrite(pair);
        _ = _broadcast.BroadcastPairAsync(pair);
    }
}
