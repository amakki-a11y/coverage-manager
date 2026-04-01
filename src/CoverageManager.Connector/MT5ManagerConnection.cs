using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;

namespace CoverageManager.Connector;

/// <summary>
/// Background service that connects to MT5 Manager API using credentials from account_settings.
/// Snapshots positions periodically and subscribes to ticks for real-time price updates.
/// Uses exponential backoff on connection failure (1s -> 60s).
/// </summary>
public sealed class MT5ManagerConnection : BackgroundService
{
    private readonly ILogger<MT5ManagerConnection> _logger;
    private readonly PositionManager _positionManager;
    private readonly PriceCache _priceCache;
    private readonly DealStore _dealStore;
    private readonly Func<Task<List<AccountSettings>>> _getAccounts;
    private readonly Action _onUpdate;

    private IMT5Api? _api;
    private ulong[] _logins = [];
    private long _tickCount;

    private const int InitialBackoffMs = 1000;
    private const int MaxBackoffMs = 60000;
    private const int PositionSnapshotIntervalMs = 500;

    public bool IsConnected => _api?.IsConnected ?? false;
    public string? ConnectedServer { get; private set; }
    public int PositionCount { get; private set; }
    public int LoginCount { get; private set; }

    public MT5ManagerConnection(
        ILogger<MT5ManagerConnection> logger,
        PositionManager positionManager,
        PriceCache priceCache,
        DealStore dealStore,
        Func<Task<List<AccountSettings>>> getAccounts,
        Action onUpdate)
    {
        _logger = logger;
        _positionManager = positionManager;
        _priceCache = priceCache;
        _dealStore = dealStore;
        _getAccounts = getAccounts;
        _onUpdate = onUpdate;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(2000, stoppingToken); // Let the rest of the app start

        var backoffMs = InitialBackoffMs;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Get active manager accounts from Supabase
                var accounts = await _getAccounts();
                var managerAccount = accounts
                    .FirstOrDefault(a => a.AccountType == "manager" && a.IsActive);

                if (managerAccount == null)
                {
                    _logger.LogInformation("No active manager account configured. Waiting...");
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }

                _logger.LogInformation(
                    "Connecting to MT5 Manager: {Label} @ {Server} login {Login}",
                    managerAccount.Label, managerAccount.Server, managerAccount.Login);

                _api = new MT5ApiReal();

                if (!_api.Initialize())
                {
                    _logger.LogError("MT5 API init failed: {Error}", _api.LastError);
                    _api.Dispose(); _api = null;
                    await Task.Delay(backoffMs, stoppingToken);
                    backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);
                    continue;
                }

                if (!_api.Connect(managerAccount.Server, (ulong)managerAccount.Login, managerAccount.Password))
                {
                    _logger.LogError("MT5 connection failed: {Error}", _api.LastError);
                    _api.Dispose(); _api = null;
                    await Task.Delay(backoffMs, stoppingToken);
                    backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);
                    continue;
                }

                ConnectedServer = managerAccount.Server;
                _logger.LogInformation("Connected to MT5 {Server} as login {Login}",
                    managerAccount.Server, managerAccount.Login);

                // Select all symbols for tick streaming
                if (!_api.SelectedAddAll())
                    _logger.LogWarning("SelectedAddAll failed: {Error}", _api.LastError);

                // Subscribe to ticks
                _api.OnTick += OnTickReceived;
                if (!_api.SubscribeTicks())
                    _logger.LogWarning("Tick subscribe failed: {Error}", _api.LastError);
                else
                    _logger.LogInformation("Subscribed to tick stream");

                // Subscribe to deals (for real-time closed trade tracking)
                _api.OnDealAdd += OnDealReceived;
                if (!_api.SubscribeDeals())
                    _logger.LogWarning("Deal subscribe failed: {Error}", _api.LastError);
                else
                    _logger.LogInformation("Subscribed to deal stream");

                // Get logins matching group mask
                var logins = _api.GetUserLogins(managerAccount.GroupMask);
                LoginCount = logins.Length;
                _logger.LogInformation("Found {Count} logins matching group mask '{Mask}'",
                    logins.Length, managerAccount.GroupMask);

                // If no logins from group mask, use the manager login itself
                if (logins.Length == 0)
                    logins = [(ulong)managerAccount.Login];

                _logins = logins;

                // Backfill today's closed deals
                BackfillDeals(logins, new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero), DateTimeOffset.UtcNow);

                backoffMs = InitialBackoffMs;

                // Position snapshot loop
                while (!stoppingToken.IsCancellationRequested && _api.IsConnected)
                {
                    SnapshotPositions(logins);
                    await Task.Delay(PositionSnapshotIntervalMs, stoppingToken);
                }

                _logger.LogWarning("MT5 connection lost. Reconnecting...");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MT5 error. Reconnecting in {BackoffMs}ms...", backoffMs);
                await Task.Delay(backoffMs, stoppingToken);
                backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);
            }
            finally
            {
                if (_api != null)
                {
                    _api.OnTick -= OnTickReceived;
                    _api.OnDealAdd -= OnDealReceived;
                    _api.UnsubscribeTicks();
                    _api.UnsubscribeDeals();
                    _api.Disconnect();
                    _api.Dispose();
                    _api = null;
                }
                ConnectedServer = null;
            }
        }

        _logger.LogInformation("MT5 Manager connection service stopped");
    }

    private void SnapshotPositions(ulong[] logins)
    {
        if (_api == null || !_api.IsConnected) return;

        try
        {
            var snapshot = new Dictionary<string, Position>();

            foreach (var login in logins)
            {
                var positions = _api.GetPositions(login);
                foreach (var pos in positions)
                {
                    var key = $"bbook:{pos.Login}:{pos.PositionId}";
                    snapshot[key] = new Position
                    {
                        Source = "bbook",
                        Login = pos.Login,
                        Symbol = pos.Symbol,
                        Direction = pos.Action == 0 ? "BUY" : "SELL",
                        VolumeLots = (decimal)pos.Volume,
                        OpenPrice = (decimal)pos.PriceOpen,
                        CurrentPrice = (decimal)pos.PriceCurrent,
                        Profit = (decimal)pos.Profit,
                        Swap = (decimal)pos.Storage,
                        OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(pos.TimeMsc).UtcDateTime,
                        UpdatedAt = DateTime.UtcNow
                    };
                }
            }

            _positionManager.SnapshotBBookPositions(snapshot);
            PositionCount = snapshot.Count;
            _onUpdate();

            _logger.LogDebug("Snapshot: {Count} B-Book positions across {Logins} logins",
                snapshot.Count, logins.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to snapshot positions");
        }
    }

    private void OnTickReceived(RawTick raw)
    {
        var count = Interlocked.Increment(ref _tickCount);
        if (count <= 5 || count % 10000 == 0)
        {
            _logger.LogInformation("Tick #{Count}: {Symbol} bid={Bid} ask={Ask}",
                count, raw.Symbol, raw.Bid, raw.Ask);
        }

        _priceCache.Update(raw.Symbol, (decimal)raw.Bid, (decimal)raw.Ask);
        _onUpdate();
    }

    private void OnDealReceived(RawDeal raw)
    {
        var deal = ConvertDeal(raw);
        _dealStore.AddDeal(deal);
        _logger.LogDebug("Deal received: #{DealId} {Symbol} {Entry} P&L={Profit}",
            raw.DealId, raw.Symbol, raw.Entry, raw.Profit);
    }

    /// <summary>
    /// Public method to reload deals for a given date range. Called from the API controller.
    /// </summary>
    public int ReloadDeals(DateTimeOffset from, DateTimeOffset to)
    {
        if (_api == null || !_api.IsConnected || _logins.Length == 0)
            return -1;
        return BackfillDeals(_logins, from, to);
    }

    private int BackfillDeals(ulong[] logins, DateTimeOffset from, DateTimeOffset to)
    {
        if (_api == null || !_api.IsConnected) return 0;

        _dealStore.Clear();
        var totalDeals = 0;

        foreach (var login in logins)
        {
            try
            {
                var deals = _api.RequestDeals(login, from, to);
                foreach (var raw in deals)
                {
                    _dealStore.AddDeal(ConvertDeal(raw));
                    totalDeals++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to backfill deals for login {Login}", login);
            }
        }

        _logger.LogInformation("Backfilled {Count} deals from {From:yyyy-MM-dd} to {To:yyyy-MM-dd} across {Logins} logins",
            totalDeals, from, to, logins.Length);

        var pnl = _dealStore.GetPnLBySymbol();
        foreach (var p in pnl.Take(5))
        {
            _logger.LogInformation("  {Symbol}: {Deals} deals, Net P&L = {PnL:F2}",
                p.Symbol, p.DealCount, p.NetPnL);
        }

        return totalDeals;
    }

    private static ClosedDeal ConvertDeal(RawDeal raw)
    {
        return new ClosedDeal
        {
            DealId = raw.DealId,
            Login = raw.Login,
            Symbol = raw.Symbol,
            Direction = raw.Action == 0 ? "BUY" : "SELL",
            VolumeLots = (decimal)raw.VolumeLots,
            Price = (decimal)raw.Price,
            Profit = (decimal)raw.Profit,
            Commission = (decimal)raw.Commission,
            Swap = (decimal)raw.Storage,
            Fee = (decimal)raw.Fee,
            Entry = raw.Entry,
            Time = DateTimeOffset.FromUnixTimeMilliseconds(raw.TimeMsc).UtcDateTime
        };
    }
}
