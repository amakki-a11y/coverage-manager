using CoverageManager.Connector;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;

namespace CoverageManager.Api.Services;

/// <summary>The feed's working set as the self-check sees it. A seam so tests can inject divergence.</summary>
public interface IFeedDealSource
{
    bool IsConnected { get; }
    /// <summary>How far back the feed's deal record reaches (never earlier than the earliest deal it holds).</summary>
    DealHistoryWindow DealHistory { get; }
    /// <summary>Trade deals (action &lt; 2) the feed holds in the given span.</summary>
    IReadOnlyList<ClosedDeal> QueryDeals(DateTimeOffset from, DateTimeOffset to);
}

/// <summary>Production source: the B-Book connection, whose <c>QueryDeals</c> answers from the Live Bridge FeedBook.</summary>
public sealed class ConnectionFeedDealSource : IFeedDealSource
{
    private readonly MT5ManagerConnection _connection;
    public ConnectionFeedDealSource(MT5ManagerConnection connection) => _connection = connection;
    public bool IsConnected => _connection.IsConnected;
    public DealHistoryWindow DealHistory => _connection.DealHistory;
    public IReadOnlyList<ClosedDeal> QueryDeals(DateTimeOffset from, DateTimeOffset to) => _connection.QueryDeals(from, to);
}

/// <summary>
/// v2 feed&lt;-&gt;store self-check (V2_PLAN §5.4; owner decision 2026-09-12). Replaces v1's heavy
/// ReconciliationService sweep, which was deleted: there is no second authority to reconcile
/// against any more. On a schedule (and on demand from Settings) it compares the feed's working
/// set with Postgres for the retained window only, re-writes missing or diverged deals from the
/// feed, and NEVER deletes. Every run is recorded in reconciliation_runs, so the Settings
/// Reconciliation card keeps working unchanged:
///   mt5_deal_count = feed deals, supabase_deal_count = stored deals, backfilled = missing
///   re-written, modified = diverged re-written, ghost_deleted = always 0, notes = the summary.
/// (The column names are v1's; the table was slimmed in place rather than renamed.)
/// </summary>
public sealed class FeedStoreSelfCheckService : BackgroundService
{
    private readonly IFeedDealSource _feed;
    private readonly IDataStore _store;
    private readonly PositionManager _positionManager;
    private readonly ILogger<FeedStoreSelfCheckService> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _startupDelay;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Only used if a provider ever reports its full history; under the feed the window is the feed's own.</summary>
    public static readonly TimeSpan FullLookback = TimeSpan.FromHours(48);
    /// <summary>Deal stamps are the MT5 server clock (UTC+3 here), so the newest deals sit ahead of UTC.</summary>
    public static readonly TimeSpan AheadBuffer = TimeSpan.FromDays(1);

    public FeedStoreSelfCheckService(
        IFeedDealSource feed,
        IDataStore store,
        PositionManager positionManager,
        IConfiguration config,
        ILogger<FeedStoreSelfCheckService> logger)
    {
        _feed = feed;
        _store = store;
        _positionManager = positionManager;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(Math.Max(1, config.GetValue("SelfCheck:IntervalMinutes", 15)));
        _startupDelay = TimeSpan.FromMinutes(Math.Max(0, config.GetValue("SelfCheck:StartupDelayMinutes", 2)));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Feed/store self-check: every {Interval} (first run after {Delay}); never deletes",
            _interval, _startupDelay);
        try { await Task.Delay(_startupDelay, stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunNowAsync("scheduled", stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Feed/store self-check tick failed"); }

            try { await Task.Delay(_interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Run one self-check now. Serialized: a manual run waits for a scheduled one in progress.</summary>
    public async Task<ReconciliationRun> RunNowAsync(string triggerType = "manual", CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var now = DateTime.UtcNow;
            var run = new ReconciliationRun
            {
                TriggerType = triggerType == "scheduled" ? "scheduled" : "manual",
                StartedAt = now,
                WindowFrom = now,
                WindowTo = now,
                GhostDeleted = 0, // the self-check never deletes
            };

            try
            {
                if (!_feed.IsConnected)
                {
                    run.Error = "feed not connected";
                    run.Notes = "skipped: the Live Bridge feed is not connected, so there is no working set to compare";
                    return await RecordAsync(run);
                }

                var window = _feed.DealHistory;
                if (window.IsEmpty)
                {
                    var skip = FeedStoreSelfCheck.Plan(Array.Empty<DealRecord>(), Array.Empty<DealRecord>(),
                        window, now, FullLookback, AheadBuffer);
                    run.Notes = skip.Note;
                    return await RecordAsync(run);
                }

                var from = window.Complete ? now - FullLookback : window.FromUtc!.Value;
                var to = now + AheadBuffer;

                var feedDeals = _feed.QueryDeals(new DateTimeOffset(from, TimeSpan.Zero), new DateTimeOffset(to, TimeSpan.Zero))
                                     .Select(d => DealRecordMapper.FromClosedDeal(d, _positionManager))
                                     .ToList();
                var storedDeals = await _store.GetDealsAsync("bbook", from, to);

                var plan = FeedStoreSelfCheck.Plan(feedDeals, storedDeals, window, now, FullLookback, AheadBuffer);
                run.WindowFrom = plan.FromUtc;
                run.WindowTo = plan.ToUtc;
                run.Mt5DealCount = plan.FeedDealCount;
                run.SupabaseDealCount = plan.StoredDealCount;
                run.Notes = plan.Note;

                if (plan.Rewrite.Count > 0)
                {
                    // Diverged values are audit-worthy: record old -> new before overwriting them.
                    if (plan.Modified.Count > 0)
                        await _store.DetectAndLogDealChangesAsync(plan.Modified, "bbook");

                    var written = await _store.UpsertDealsAsync(plan.Rewrite);
                    if (written == plan.Rewrite.Count)
                    {
                        run.Backfilled = plan.Missing.Count;
                        run.Modified = plan.Modified.Count;
                        _logger.LogWarning("Feed/store self-check re-wrote {Missing} missing and {Modified} modified deal(s); {StoreOnly} store-only kept",
                            plan.Missing.Count, plan.Modified.Count, plan.StoreOnly);
                    }
                    else
                    {
                        run.Error = $"re-write incomplete: {written} of {plan.Rewrite.Count} rows written";
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Feed/store self-check failed");
                run.Error = ex.Message;
            }

            return await RecordAsync(run);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ReconciliationRun> RecordAsync(ReconciliationRun run)
    {
        run.FinishedAt = DateTime.UtcNow;
        var saved = await _store.InsertReconciliationRunAsync(run);
        return saved ?? run;
    }
}
