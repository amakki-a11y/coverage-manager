using Cronos;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;

namespace CoverageManager.Api.Services;

/// <summary>
/// Dispatches the snapshot scheduler for the Period P&L feature.
///
/// Once per minute:
///   1. Load snapshot_schedules where enabled AND next_run_at &lt;= now().
///      For rows with no next_run_at set yet, compute it.
///   2. For each due schedule, call <see cref="CaptureOnceAsync"/> which runs
///      ExposureEngine and upserts one row per canonical symbol into
///      exposure_snapshots.
///   3. Update last_run_at + next_run_at on the schedule.
///
/// Also exposes <see cref="RunNowAsync"/> for on-demand captures from the UI.
/// </summary>
public class ExposureSnapshotService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ExposureSnapshotService> _logger;
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);

    public ExposureSnapshotService(
        IServiceProvider services,
        ILogger<ExposureSnapshotService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExposureSnapshotService starting");
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogWarning(ex, "Snapshot scheduler tick failed"); }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
        _logger.LogInformation("ExposureSnapshotService stopped");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var supabase = _services.GetRequiredService<SupabaseService>();
        var schedules = await supabase.GetSnapshotSchedulesAsync();
        var now = DateTime.UtcNow;

        foreach (var s in schedules)
        {
            if (!s.Enabled || !s.Id.HasValue) continue;

            // Compute next_run_at lazily if the row has none yet (fresh install or new schedule).
            if (!s.NextRunAt.HasValue)
            {
                var next = ComputeNextRun(s, now);
                if (next.HasValue)
                {
                    s.NextRunAt = next;
                    await supabase.UpsertSnapshotScheduleAsync(s);
                }
                continue;
            }

            if (s.NextRunAt.Value > now) continue; // not yet due

            // Run
            try
            {
                await CaptureOnceAsync(s.Cadence, label: $"auto:{s.Name}", ct);
                s.LastRunAt = DateTime.UtcNow;
                s.NextRunAt = ComputeNextRun(s, s.LastRunAt.Value);
                await supabase.UpsertSnapshotScheduleAsync(s);
                _logger.LogInformation("Snapshot schedule '{Name}' ran; next run {Next:o}", s.Name, s.NextRunAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled capture '{Name}' failed", s.Name);
            }
        }
    }

    /// <summary>
    /// Capture a snapshot right now. Call from a controller for on-demand runs.
    /// Returns the number of symbol rows upserted.
    /// </summary>
    public async Task<int> RunNowAsync(string triggerType = "manual", string label = "", CancellationToken ct = default)
    {
        return await CaptureOnceAsync(triggerType, label, ct);
    }

    private async Task<int> CaptureOnceAsync(string triggerType, string label, CancellationToken ct)
    {
        var engine = _services.GetRequiredService<ExposureEngine>();
        var supabase = _services.GetRequiredService<SupabaseService>();

        var summaries = engine.CalculateExposure();
        var nowUtc = DateTime.UtcNow;
        var snapshots = summaries.Select(s => new ExposureSnapshot
        {
            CanonicalSymbol = s.CanonicalSymbol,
            SnapshotTime = nowUtc,
            BBookBuyVolume = s.BBookBuyVolume,
            BBookSellVolume = s.BBookSellVolume,
            CoverageBuyVolume = s.CoverageBuyVolume,
            CoverageSellVolume = s.CoverageSellVolume,
            NetVolume = s.NetVolume,
            BBookPnL = s.BBookPnL,
            CoveragePnL = s.CoveragePnL,
            NetPnL = s.NetPnL,
            TriggerType = triggerType,
            Label = label,
        }).ToList();

        if (snapshots.Count == 0)
        {
            _logger.LogInformation("CaptureOnceAsync ({Trigger}): no live exposure summaries to snapshot", triggerType);
            return 0;
        }

        var written = await supabase.UpsertExposureSnapshotsAsync(snapshots);
        _logger.LogInformation("Snapshot captured ({Trigger}): {Count} symbol rows", triggerType, written);
        return written;
    }

    /// <summary>
    /// Compute the next fire time for a schedule given its cadence and timezone.
    /// Daily/weekly/monthly map to canonical cron expressions. Custom uses cron_expr.
    /// </summary>
    private static DateTime? ComputeNextRun(SnapshotSchedule s, DateTime fromUtc)
    {
        var cronStr = s.Cadence switch
        {
            "daily" => s.CronExpr ?? "0 0 * * *",
            "weekly" => s.CronExpr ?? "0 0 * * 1",
            "monthly" => s.CronExpr ?? "0 0 1 * *",
            "custom" => s.CronExpr,
            _ => null,
        };
        if (string.IsNullOrWhiteSpace(cronStr)) return null;

        CronExpression expr;
        try { expr = CronExpression.Parse(cronStr); }
        catch { return null; }

        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(s.Tz); }
        catch { tz = TimeZoneInfo.Utc; }

        // GetNextOccurrence returns UTC when given a tz.
        return expr.GetNextOccurrence(fromUtc, tz);
    }
}
