using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;

namespace CoverageManager.Api.Services;

/// <summary>
/// Background service that syncs deals and accounts to Supabase periodically.
/// Detects modifications to deals and logs them to the audit trail.
/// </summary>
public sealed class DataSyncService : BackgroundService
{
    private readonly SupabaseService _supabase;
    private readonly DealStore _dealStore;
    private readonly ILogger<DataSyncService> _logger;

    private const int SyncIntervalMs = 30_000; // Sync every 30 seconds

    public DataSyncService(
        SupabaseService supabase,
        DealStore dealStore,
        ILogger<DataSyncService> logger)
    {
        _supabase = supabase;
        _dealStore = dealStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(5000, stoppingToken); // Let other services start first
        _logger.LogInformation("DataSyncService started — syncing deals to Supabase every {Interval}s", SyncIntervalMs / 1000);

        // Load today's deals from Supabase into DealStore on startup
        await LoadDealsFromSupabaseAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncDealsToSupabaseAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DataSyncService error during sync cycle");
            }

            await Task.Delay(SyncIntervalMs, stoppingToken);
        }

        _logger.LogInformation("DataSyncService stopped");
    }

    /// <summary>
    /// On startup, load today's deals from Supabase into the in-memory DealStore.
    /// This ensures deal data survives backend restarts.
    /// </summary>
    private async Task LoadDealsFromSupabaseAsync()
    {
        try
        {
            var todayStart = DateTime.UtcNow.Date;
            var todayEnd = todayStart.AddDays(1);

            var deals = await _supabase.GetDealsAsync("bbook", todayStart, todayEnd);

            if (deals.Count > 0)
            {
                var closedDeals = deals.Select(d => new ClosedDeal
                {
                    DealId = (ulong)d.DealId,
                    Login = (ulong)d.Login,
                    Symbol = d.Symbol,
                    Direction = d.Direction,
                    VolumeLots = d.Volume,
                    Price = d.Price,
                    Profit = d.Profit,
                    Commission = d.Commission,
                    Swap = d.Swap,
                    Fee = d.Fee,
                    Entry = (uint)d.Entry,
                    Time = d.DealTime
                });

                _dealStore.AddDeals(closedDeals);
                _logger.LogInformation("Loaded {Count} deals from Supabase into DealStore on startup", deals.Count);
            }
            else
            {
                _logger.LogInformation("No deals found in Supabase for today");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load deals from Supabase on startup");
        }
    }

    /// <summary>
    /// Sync current in-memory deals to Supabase.
    /// Detects any modifications (dealer edits) and logs to audit trail.
    /// </summary>
    private async Task SyncDealsToSupabaseAsync()
    {
        var allDeals = _dealStore.GetAllDeals();
        if (allDeals.Count == 0) return;

        var dealRecords = allDeals.Select(d => new DealRecord
        {
            DealId = (long)d.DealId,
            Source = "bbook",
            Login = (long)d.Login,
            Symbol = d.Symbol,
            CanonicalSymbol = d.Symbol, // TODO: resolve via symbol mappings if needed
            Direction = d.Direction,
            Action = d.Direction == "BUY" ? 0 : 1,
            Entry = (int)d.Entry,
            Volume = d.VolumeLots,
            Price = d.Price,
            Profit = d.Profit,
            Commission = d.Commission,
            Swap = d.Swap,
            Fee = d.Fee,
            DealTime = d.Time
        }).ToList();

        // Detect changes (compare incoming vs stored in Supabase)
        var changes = await _supabase.DetectAndLogDealChangesAsync(dealRecords, "bbook");
        if (changes > 0)
        {
            _logger.LogWarning("⚠ Detected {Count} deal modifications by dealer/admin", changes);
        }

        // Upsert deals to Supabase
        await _supabase.UpsertDealsAsync(dealRecords);
    }
}
