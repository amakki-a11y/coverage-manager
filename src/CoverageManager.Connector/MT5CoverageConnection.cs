using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;

namespace CoverageManager.Connector;

/// <summary>
/// Background service that connects to MT5 coverage (LP) accounts via Manager API.
/// Snapshots positions and feeds them into PositionManager as "coverage" source.
/// </summary>
public sealed class MT5CoverageConnection : BackgroundService
{
    private readonly ILogger<MT5CoverageConnection> _logger;
    private readonly PositionManager _positionManager;
    private readonly PriceCache _priceCache;
    private readonly Func<Task<List<AccountSettings>>> _getAccounts;
    private readonly Action _onUpdate;

    // Only assigned when MT5_MANAGER_COVERAGE_ENABLED is defined at build time.
    // Suppress CS0649 ("never assigned") for the default build where the gated
    // ExecuteAsync block is excluded.
#pragma warning disable CS0649
    private IMT5Api? _api;
#pragma warning restore CS0649
    private long _tickCount;

    private const int InitialBackoffMs = 1000;
    private const int MaxBackoffMs = 60000;
    private const int PositionSnapshotIntervalMs = 500;

    public bool IsConnected => _api?.IsConnected ?? false;
    public string? ConnectedServer { get; private set; }
    public int PositionCount { get; private set; }

    public MT5CoverageConnection(
        ILogger<MT5CoverageConnection> logger,
        PositionManager positionManager,
        PriceCache priceCache,
        Func<Task<List<AccountSettings>>> getAccounts,
        Action onUpdate)
    {
        _logger = logger;
        _positionManager = positionManager;
        _priceCache = priceCache;
        _getAccounts = getAccounts;
        _onUpdate = onUpdate;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Coverage positions are handled by the Python collector (MT5 Terminal),
        // NOT via Manager API. This service is kept for future Manager API coverage
        // accounts but currently stays idle.
        _logger.LogInformation("[Coverage] Manager API coverage service disabled — use Python collector for terminal-based LP accounts");

        // Just wait until shutdown — no active work
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }

        _logger.LogInformation("[Coverage] Connection service stopped");
        return;

        // ---- Original Manager API logic (kept for reference) ----
        // Toggle on by defining the symbol at project level if Manager API coverage
        // is ever re-enabled. Under the default build this block compiles as a no-op,
        // which also suppresses the CS0162 "unreachable code" warning the unguarded
        // version produced.
#if MT5_MANAGER_COVERAGE_ENABLED
        await Task.Delay(3000, stoppingToken); // Let manager connection start first

        var backoffMs = InitialBackoffMs;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var accounts = await _getAccounts();
                var coverageAccount = accounts
                    .FirstOrDefault(a => a.AccountType == "coverage" && a.IsActive);

                if (coverageAccount == null)
                {
                    _logger.LogInformation("[Coverage] No active coverage account configured. Waiting...");
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }

                _logger.LogInformation(
                    "[Coverage] Connecting to LP: {Label} @ {Server} login {Login}",
                    coverageAccount.Label, coverageAccount.Server, coverageAccount.Login);

                _api = new MT5ApiReal();

                if (!_api.Initialize())
                {
                    _logger.LogError("[Coverage] MT5 API init failed: {Error}", _api.LastError);
                    _api.Dispose(); _api = null;
                    await Task.Delay(backoffMs, stoppingToken);
                    backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);
                    continue;
                }

                if (!_api.Connect(coverageAccount.Server, (ulong)coverageAccount.Login, coverageAccount.Password))
                {
                    _logger.LogError("[Coverage] Connection failed: {Error}", _api.LastError);
                    _api.Dispose(); _api = null;
                    await Task.Delay(backoffMs, stoppingToken);
                    backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);
                    continue;
                }

                ConnectedServer = coverageAccount.Server;
                _logger.LogInformation("[Coverage] Connected to LP {Server} as login {Login}",
                    coverageAccount.Server, coverageAccount.Login);

                // Subscribe to ticks
                _api.OnTick += OnTickReceived;
                if (_api.SelectedAddAll())
                    _logger.LogInformation("[Coverage] Selected all symbols for tick stream");
                if (_api.SubscribeTicks())
                    _logger.LogInformation("[Coverage] Subscribed to tick stream");

                backoffMs = InitialBackoffMs;

                // Position snapshot loop — coverage positions for this single login
                while (!stoppingToken.IsCancellationRequested && _api.IsConnected)
                {
                    SnapshotCoveragePositions((ulong)coverageAccount.Login);
                    await Task.Delay(PositionSnapshotIntervalMs, stoppingToken);
                }

                _logger.LogWarning("[Coverage] Connection lost. Reconnecting...");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Coverage] Error. Reconnecting in {BackoffMs}ms...", backoffMs);
                await Task.Delay(backoffMs, stoppingToken);
                backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);
            }
            finally
            {
                if (_api != null)
                {
                    _api.OnTick -= OnTickReceived;
                    _api.UnsubscribeTicks();
                    _api.Disconnect();
                    _api.Dispose();
                    _api = null;
                }
                ConnectedServer = null;
            }
        }

        _logger.LogInformation("[Coverage] Connection service stopped");
#endif
    }

    private void SnapshotCoveragePositions(ulong login)
    {
        if (_api == null || !_api.IsConnected) return;

        try
        {
            var positions = _api.GetPositions(login);
            var dtos = positions.Select(pos => new CoveragePositionDto
            {
                Symbol = pos.Symbol,
                Direction = pos.Action == 0 ? "BUY" : "SELL",
                Volume = (decimal)pos.Volume,
                OpenPrice = (decimal)pos.PriceOpen,
                CurrentPrice = (decimal)pos.PriceCurrent,
                Profit = (decimal)pos.Profit,
                Swap = (decimal)pos.Storage,
                Ticket = (long)pos.PositionId
            }).ToList();

            _positionManager.UpdateCoveragePositions(dtos);
            PositionCount = dtos.Count;
            _onUpdate();

            _logger.LogDebug("[Coverage] Snapshot: {Count} positions for login {Login}",
                dtos.Count, login);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Coverage] Failed to snapshot positions");
        }
    }

    private void OnTickReceived(RawTick raw)
    {
        var count = Interlocked.Increment(ref _tickCount);
        if (count <= 3 || count % 10000 == 0)
        {
            _logger.LogInformation("[Coverage] Tick #{Count}: {Symbol} bid={Bid} ask={Ask}",
                count, raw.Symbol, raw.Bid, raw.Ask);
        }

        _priceCache.Update(raw.Symbol, raw.Bid, raw.Ask);
    }
}
