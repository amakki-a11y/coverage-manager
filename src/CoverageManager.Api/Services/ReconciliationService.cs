using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;
using CoverageManager.Connector;

namespace CoverageManager.Api.Services;

/// <summary>
/// Nightly deal reconciliation sweep. Ensures Supabase's `deals` table stays
/// aligned with the authoritative MT5 Manager state by:
///   1. Backfilling deals that exist in MT5 but not in Supabase (new/missed).
///   2. Deleting "ghost" deals — rows in Supabase whose deal_id no longer
///      appears in MT5 (dealer reversals, compliance removals, reassignments).
///   3. Propagating modifications — MT5 sometimes mutates a deal's price/profit
///      after the fact; DetectAndLogDealChangesAsync (called by DataSyncService
///      on live ingest) only logs these; the sweep's upsert path patches them.
///
/// Each run is persisted to `reconciliation_runs` for the Settings UI.
/// </summary>
public class ReconciliationService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ReconciliationService> _logger;
    // Loop every 10 min; schedule fires once per UTC day at 02:05 UTC.
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(10);
    // Default lookback for the nightly sweep. 14 days is plenty for dealer edits to
    // stabilize; going much larger re-scans cold data.
    private static readonly TimeSpan DefaultLookback = TimeSpan.FromDays(14);

    public ReconciliationService(IServiceProvider services, ILogger<ReconciliationService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReconciliationService starting");
        // Seed lastNightlyRun from the most recent successful scheduled run in Supa so we
        // don't re-fire on every restart after 02:05 UTC. Crucially also avoids sweeping
        // before MT5 has had a chance to connect.
        DateTime? lastNightlyRun = await LoadLastScheduledRunAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nowUtc = DateTime.UtcNow;
                // Fire once per UTC day if we crossed 02:05 UTC since last run.
                var todayTrigger = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 2, 5, 0, DateTimeKind.Utc);
                if (nowUtc >= todayTrigger && (lastNightlyRun == null || lastNightlyRun < todayTrigger))
                {
                    await RunSweepAsync("scheduled", nowUtc.AddDays(-14), nowUtc, stoppingToken);
                    lastNightlyRun = nowUtc;
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogWarning(ex, "Reconciliation tick failed"); }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
        _logger.LogInformation("ReconciliationService stopped");
    }

    private async Task<DateTime?> LoadLastScheduledRunAsync(CancellationToken ct)
    {
        try
        {
            var supabase = _services.GetRequiredService<SupabaseService>();
            var recent = await supabase.ListReconciliationRunsAsync(5);
            var lastScheduled = recent
                .Where(r => r.TriggerType == "scheduled" && string.IsNullOrEmpty(r.Error))
                .OrderByDescending(r => r.StartedAt)
                .FirstOrDefault();
            if (lastScheduled != null)
            {
                return lastScheduled.StartedAt;
            }
            // No prior successful scheduled run found — skip today's trigger if we're already past it.
            // Otherwise a stale DB would re-fire on every restart. Wait for the next UTC day.
            var nowUtc = DateTime.UtcNow;
            var todayTrigger = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 2, 5, 0, DateTimeKind.Utc);
            return nowUtc >= todayTrigger ? nowUtc : (DateTime?)null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load last reconciliation run — will run today");
            return null;
        }
    }

    /// <summary>
    /// Run a reconciliation sweep now. Used by the manual "Run Now" button.
    /// Defaults to the last 14 days when no window is provided.
    /// </summary>
    public async Task<ReconciliationRun> RunNowAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken ct = default)
    {
        var from = fromUtc ?? DateTime.UtcNow - DefaultLookback;
        var to = toUtc ?? DateTime.UtcNow;
        return await RunSweepAsync("manual", from, to, ct);
    }

    private async Task<ReconciliationRun> RunSweepAsync(
        string triggerType,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct)
    {
        var run = new ReconciliationRun
        {
            TriggerType = triggerType,
            WindowFrom = fromUtc,
            WindowTo = toUtc,
            StartedAt = DateTime.UtcNow,
        };

        var supabase = _services.GetRequiredService<SupabaseService>();
        var mt5 = _services.GetRequiredService<MT5ManagerConnection>();

        try
        {
            if (!mt5.IsConnected)
            {
                run.Error = "MT5 not connected";
                run.FinishedAt = DateTime.UtcNow;
                await supabase.InsertReconciliationRunAsync(run);
                return run;
            }

            // 1. Pull MT5 side (authoritative).
            //
            // MT5 interprets the request window as server-local time, not UTC (see
            // MT5ApiReal.RequestDeals + section 5 below). Query with a ±24h buffer so we
            // capture deals at both UTC-window edges regardless of server TZ, then filter
            // by actual UTC deal_time for the apples-to-apples comparison.
            var mt5Raw = mt5.QueryDeals(new DateTimeOffset(fromUtc.AddHours(-24), TimeSpan.Zero),
                                         new DateTimeOffset(toUtc.AddHours(+24),  TimeSpan.Zero));
            var mt5Deals = mt5Raw.Where(d => d.Time >= fromUtc && d.Time < toUtc).ToList();
            run.Mt5DealCount = mt5Deals.Count;

            // 2. Pull Supabase side (exclude moved accounts).
            var supaDeals = await supabase.GetDealsAsync("bbook", fromUtc, toUtc);
            var moved = await supabase.GetMovedLoginsAsync();
            var supaTrade = supaDeals
                .Where(d => d.Action <= 1 && !string.IsNullOrEmpty(d.Symbol))
                .Where(d => !moved.Contains(d.Login))
                .ToList();
            run.SupabaseDealCount = supaTrade.Count;

            var mt5Ids = new HashSet<long>(mt5Deals.Select(d => (long)d.DealId));
            var supaIds = new HashSet<long>(supaTrade.Select(d => d.DealId));

            // 3. Backfill — MT5 has, Supa doesn't. mt5Deals is already UTC-window-filtered.
            var missingMt5 = mt5Deals.Where(d => !supaIds.Contains((long)d.DealId)).ToList();
            if (missingMt5.Count > 0)
            {
                var sample = missingMt5.Take(10).Select(d => $"{d.DealId}:{d.Login}:{d.Symbol}@{d.Time:o}").ToList();
                _logger.LogInformation("Reconciliation: {N} missing (MT5 has, Supa lacks). Sample: {Sample}", missingMt5.Count, string.Join(" ; ", sample));
                var records = missingMt5.Select(d => new DealRecord
                {
                    DealId = (long)d.DealId,
                    Source = "bbook",
                    Login = (long)d.Login,
                    Symbol = d.Symbol,
                    CanonicalSymbol = d.Symbol,
                    Direction = d.Direction,
                    Action = d.Direction == "BUY" ? 0 : 1,
                    Entry = (int)d.Entry,
                    Volume = d.VolumeLots,
                    Price = d.Price,
                    Profit = d.Profit,
                    Commission = d.Commission,
                    Swap = d.Swap,
                    Fee = d.Fee,
                    OrderId = d.OrderId == 0 ? null : (long?)d.OrderId,
                    PositionId = d.PositionId == 0 ? null : (long?)d.PositionId,
                    DealTime = d.Time,
                }).ToList();
                run.Backfilled = await supabase.UpsertDealsAsync(records);
            }

            // 4. Modifications — deals whose fields differ. Detect logs to trade_audit_log
            //    and the subsequent UpsertDealsAsync patches the Supa row.
            var commonDeals = mt5Deals
                .Where(d => supaIds.Contains((long)d.DealId))
                .Select(d => new DealRecord
                {
                    DealId = (long)d.DealId,
                    Source = "bbook",
                    Login = (long)d.Login,
                    Symbol = d.Symbol,
                    CanonicalSymbol = d.Symbol,
                    Direction = d.Direction,
                    Action = d.Direction == "BUY" ? 0 : 1,
                    Entry = (int)d.Entry,
                    Volume = d.VolumeLots,
                    Price = d.Price,
                    Profit = d.Profit,
                    Commission = d.Commission,
                    Swap = d.Swap,
                    Fee = d.Fee,
                    OrderId = d.OrderId == 0 ? null : (long?)d.OrderId,
                    PositionId = d.PositionId == 0 ? null : (long?)d.PositionId,
                    DealTime = d.Time,
                }).ToList();

            if (commonDeals.Count > 0)
            {
                run.Modified = await supabase.DetectAndLogDealChangesAsync(commonDeals, "bbook");
                if (run.Modified > 0)
                {
                    // Patch the rows by re-upserting the MT5 authoritative version.
                    await supabase.UpsertDealsAsync(commonDeals);
                }
            }

            // 5. Ghost deletion — Supa has, MT5 doesn't.
            // MT5 interprets DealRequest windows as server-local, not UTC, so we query
            // with a ±24h buffer above and filter both sides to UTC deal_time ∈ [fromUtc,
            // toUtc). That makes the set comparison apples-to-apples regardless of server
            // TZ.
            var ghosts = supaTrade.Where(d => !mt5Ids.Contains(d.DealId)).ToList();
            if (ghosts.Count > 0)
            {
                var sample = ghosts.Take(10).Select(d => $"{d.DealId}:{d.Login}:{d.Symbol}@{d.DealTime:o}").ToList();
                _logger.LogInformation("Reconciliation: {N} ghosts (Supa has, MT5 lacks). Sample: {Sample}", ghosts.Count, string.Join(" ; ", sample));
                run.GhostDeleted = await supabase.DeleteDealsAsync("bbook", ghosts.Select(d => d.DealId));

                // Also evict from in-memory DealStore so DataSyncService (30s tick) doesn't re-upsert them.
                var dealStore = _services.GetRequiredService<DealStore>();
                var evicted = dealStore.RemoveDeals(ghosts.Select(d => (ulong)d.DealId));
                if (evicted > 0)
                    _logger.LogInformation("Reconciliation: evicted {Count} ghost deals from in-memory DealStore", evicted);
            }

            run.Notes = $"backfill={run.Backfilled}, ghost={run.GhostDeleted}, modified={run.Modified}";
            run.FinishedAt = DateTime.UtcNow;
            _logger.LogInformation(
                "Reconciliation sweep ({Trigger}) complete: backfill={Back}, ghost={Ghost}, modified={Mod} (window {From:o}..{To:o})",
                triggerType, run.Backfilled, run.GhostDeleted, run.Modified, fromUtc, toUtc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reconciliation sweep failed");
            run.Error = ex.Message;
            run.FinishedAt = DateTime.UtcNow;
        }

        await supabase.InsertReconciliationRunAsync(run);
        return run;
    }
}
