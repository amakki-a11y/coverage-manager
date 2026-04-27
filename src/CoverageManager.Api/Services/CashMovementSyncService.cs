using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CoverageManager.Connector;
using CoverageManager.Core.Models;

namespace CoverageManager.Api.Services;

/// <summary>
/// Background service that periodically backfills MT5 admin balance/credit
/// deals (action ≥ 2) into Supabase.
///
/// <para><b>Why this exists:</b> Admin balance/credit transfers performed via
/// the MT5 Manager UI do NOT reliably trigger <c>CIMTDealSink.OnDealAdd</c>
/// callbacks. The nightly <see cref="ReconciliationService"/> filters its
/// <c>QueryDeals</c> output to <c>action ≤ 1</c> (trade deals only) so it
/// won't catch these either. Without this service, the only path that
/// captures admin cash movements is the manual <c>POST
/// /api/equity-pnl/backfill-cash-movements</c> button in Settings — which
/// means the Equity P&amp;L tab's Net Dep/W and Net Cred columns silently
/// lag by however long it's been since a dealer remembered to click it.
/// In practice we found multi-week gaps with five-figure missing credits.</para>
///
/// <para><b>Cadence:</b> every <see cref="SyncIntervalMinutes"/> minutes,
/// sliding <see cref="LookbackDays"/>-day window. The first run waits two
/// minutes after startup so the MT5 connection has time to establish and
/// the initial deal backfill has flushed to Supabase. Each per-login MT5
/// call is paced 300ms apart — same rate as the manual endpoint — to keep
/// the native side from bursting under sustained load.</para>
///
/// <para><b>Idempotency:</b> upserts on <c>(source, deal_id)</c>; running it
/// twice in a row is a no-op for unchanged deals. Doesn't delete ghosts —
/// that responsibility stays with <see cref="ReconciliationService"/>.</para>
/// </summary>
public sealed class CashMovementSyncService : BackgroundService
{
    private readonly ILogger<CashMovementSyncService> _logger;
    private readonly MT5ManagerConnection _mt5;
    private readonly SupabaseService _supabase;

    private const int SyncIntervalMinutes = 15;
    private const int LookbackDays = 7;
    private const int PerLoginPacingMs = 300;
    private const int StartupDelayMinutes = 2;

    public CashMovementSyncService(
        ILogger<CashMovementSyncService> logger,
        MT5ManagerConnection mt5,
        SupabaseService supabase)
    {
        _logger = logger;
        _mt5 = mt5;
        _supabase = supabase;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "CashMovementSyncService starting (interval {Min}m, lookback {Days}d, per-login pacing {Pace}ms)",
            SyncIntervalMinutes, LookbackDays, PerLoginPacingMs);

        try { await Task.Delay(TimeSpan.FromMinutes(StartupDelayMinutes), stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_mt5.IsConnected)
                    await RunOnceAsync(stoppingToken).ConfigureAwait(false);
                else
                    _logger.LogDebug("CashMovementSync skipped — MT5 not connected");
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CashMovementSync tick failed");
            }

            try { await Task.Delay(TimeSpan.FromMinutes(SyncIntervalMinutes), stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("CashMovementSyncService stopped");
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var fromUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-LookbackDays), DateTimeKind.Utc);
        var toUtc   = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(1), DateTimeKind.Utc);

        var accounts = await _supabase.GetTradingAccountsAsync("bbook").ConfigureAwait(false);
        var moved    = await _supabase.GetMovedLoginsAsync().ConfigureAwait(false);
        accounts = accounts
            .Where(a => a.Status == "active" && !moved.Contains(a.Login))
            .ToList();

        var totalFetched = 0;
        var totalPersisted = 0;
        var errors = 0;

        foreach (var acct in accounts)
        {
            if (ct.IsCancellationRequested) break;

            List<ClosedDeal> deals;
            try
            {
                deals = _mt5.QueryAllDealsForLogin((ulong)acct.Login,
                    new DateTimeOffset(fromUtc), new DateTimeOffset(toUtc));
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogWarning(ex, "Cash-movement sync failed for {Login}", acct.Login);
                try { await Task.Delay(PerLoginPacingMs, ct).ConfigureAwait(false); } catch { }
                continue;
            }

            // Only non-trade deals — trade deals are already covered by
            // DataSyncService's 30s flush of the in-memory DealStore.
            var nonTrade = deals.Where(d => d.Action >= 2).ToList();
            totalFetched += nonTrade.Count;

            if (nonTrade.Count > 0)
            {
                var records = nonTrade.Select(d => new DealRecord
                {
                    Source = "bbook",
                    DealId = (long)d.DealId,
                    Login = (long)d.Login,
                    Symbol = d.Symbol ?? string.Empty,
                    CanonicalSymbol = d.Symbol ?? string.Empty,
                    Direction = d.Direction ?? string.Empty,
                    Action = (int)d.Action,
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
                totalPersisted += await _supabase.UpsertDealsAsync(records).ConfigureAwait(false);
            }

            try { await Task.Delay(PerLoginPacingMs, ct).ConfigureAwait(false); } catch { break; }
        }

        _logger.LogInformation(
            "CashMovementSync: {Logins} logins, {Fetched} non-trade deals fetched, {Persisted} persisted, {Errors} errors",
            accounts.Count, totalFetched, totalPersisted, errors);
    }
}
