using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;

namespace CoverageManager.Api.Services;

/// <summary>
/// Nightly 12-month deal-retention pruner (owner decision 2026-09-12, V2_PLAN §5.5). Runs the rule
/// documented in db/README.md -- delete deals with <c>deal_time &lt; UTC-midnight-today - 12 months</c>
/// -- and nothing else: no other table, no other predicate.
///
/// <para>Safety:
///   * a configured <c>Retention:Months</c> below the decided 12 is REFUSED -- the run is recorded with an
///     error and nothing is deleted (a mistyped 1 would otherwise wipe eleven months of history);
///   * the cutoff is UTC midnight, so the boundary never moves during a day and matches the import
///     tooling's window exactly;
///   * deletes go in batches, so a first prune over a large backlog never holds one long table lock;
///   * every run -- success, refusal or failure -- is written to retention_prune_runs and logged.</para>
///
/// <para>Scheduled at <c>Retention:PruneAtUtc</c> (HH:mm, default 03:15 UTC -- after v1's old 02:05 sweep
/// slot and outside market hours).</para>
/// </summary>
public sealed class DealRetentionPruneService : BackgroundService
{
    private readonly IDataStore _store;
    private readonly ILogger<DealRetentionPruneService> _logger;
    private readonly int _months;
    private readonly TimeSpan _pruneAtUtc;
    private readonly int _batchSize;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DealRetentionPruneService(IDataStore store, IConfiguration config, ILogger<DealRetentionPruneService> logger)
    {
        _store = store;
        _logger = logger;
        _months = config.GetValue("Retention:Months", RetentionPolicy.DecidedMonths);
        _batchSize = config.GetValue("Retention:BatchSize", 20_000);
        _pruneAtUtc = TimeSpan.TryParse(config["Retention:PruneAtUtc"], out var at) && at >= TimeSpan.Zero && at < TimeSpan.FromDays(1)
            ? at
            : new TimeSpan(3, 15, 0);
    }

    /// <summary>The next scheduled run strictly after <paramref name="nowUtc"/>.</summary>
    public static DateTime NextRunUtc(DateTime nowUtc, TimeSpan atUtc)
    {
        var today = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, DateTimeKind.Utc) + atUtc;
        return today > nowUtc ? today : today.AddDays(1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Deal retention pruner: keep {Months} months, nightly at {At} UTC",
            _months, _pruneAtUtc.ToString(@"hh\:mm"));
        while (!stoppingToken.IsCancellationRequested)
        {
            var next = NextRunUtc(DateTime.UtcNow, _pruneAtUtc);
            try { await Task.Delay(next - DateTime.UtcNow, stoppingToken); }
            catch (OperationCanceledException) { break; }

            try { await RunNowAsync("scheduled", DateTime.UtcNow, stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Deal retention prune tick failed"); }
        }
    }

    /// <summary>Run one prune as of <paramref name="nowUtc"/>. Serialized with any run in progress.</summary>
    public async Task<RetentionPruneRun> RunNowAsync(string triggerType, DateTime nowUtc, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var run = new RetentionPruneRun
            {
                TriggerType = triggerType == "scheduled" ? "scheduled" : "manual",
                RetentionMonths = _months,
                StartedAt = DateTime.UtcNow,
                CutoffUtc = RetentionPolicy.CutoffUtc(nowUtc, Math.Max(_months, RetentionPolicy.DecidedMonths)),
            };

            if (RetentionPolicy.IsBelowDecided(_months))
            {
                run.Error = $"refused: Retention:Months={_months} is below the decided {RetentionPolicy.DecidedMonths}; nothing deleted";
                _logger.LogError("Deal retention prune REFUSED: Retention:Months={Months} is below the decided {Decided}; nothing deleted",
                    _months, RetentionPolicy.DecidedMonths);
                return await RecordAsync(run);
            }

            try
            {
                var (deleted, batches) = await _store.DeleteDealsOlderThanAsync(run.CutoffUtc, _batchSize, ct);
                run.Deleted = deleted;
                run.Batches = batches;
                run.Notes = $"deleted deals with deal_time < {run.CutoffUtc:O} (keep {_months} months)";
                _logger.LogInformation("Deal retention prune: deleted {Deleted} deal(s) older than {Cutoff:O} in {Batches} batch(es)",
                    deleted, run.CutoffUtc, batches);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                run.Error = ex.Message;
                _logger.LogError(ex, "Deal retention prune failed (cutoff {Cutoff:O})", run.CutoffUtc);
            }

            return await RecordAsync(run);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<RetentionPruneRun> RecordAsync(RetentionPruneRun run)
    {
        run.FinishedAt = DateTime.UtcNow;
        return await _store.InsertRetentionPruneRunAsync(run) ?? run;
    }
}
