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
    private readonly Action<string>? _onPriceTick;
    private readonly Action<ClosedDeal>? _onDealSettled;
    private readonly Func<IEnumerable<TradingAccount>, Task>? _syncAccounts;
    private readonly Func<string, Task<DateTime?>>? _getLastDealTime;

    private IMT5Api? _api;
    private ulong[] _logins = [];
    private long _tickCount;
    private DateTime _lastAccountSync = DateTime.MinValue;

    // Stage 2a diagnostics — events are now authoritative for PositionManager,
    // but the 500 ms poll still runs and reports drift (event cache vs poll
    // snapshot mismatch) so we can catch missed events. Stage 2b drops the poll
    // to 60 s once /api/exposure/diagnostics shows driftCount ≈ 0 over 24 h.
    private long _positionAddCount;
    private long _positionUpdateCount;
    private long _positionDeleteCount;
    private long _userUpdateCount;
    private long _snapshotCount;
    private long _driftCount;
    private long _driftPollCount;
    private DateTime _lastPositionEventAt = DateTime.MinValue;
    private DateTime _lastUserEventAt = DateTime.MinValue;
    private DateTime _lastTickAt = DateTime.MinValue;
    private DateTime _lastSnapshotAt = DateTime.MinValue;
    private DateTime _lastDriftAt = DateTime.MinValue;

    public long PositionAddCount => Interlocked.Read(ref _positionAddCount);
    public long PositionUpdateCount => Interlocked.Read(ref _positionUpdateCount);
    public long PositionDeleteCount => Interlocked.Read(ref _positionDeleteCount);
    public long UserUpdateCount => Interlocked.Read(ref _userUpdateCount);
    public long TickCount => Interlocked.Read(ref _tickCount);
    public long SnapshotCount => Interlocked.Read(ref _snapshotCount);
    public long DriftCount => Interlocked.Read(ref _driftCount);
    public long DriftPollCount => Interlocked.Read(ref _driftPollCount);
    public DateTime LastPositionEventAt => _lastPositionEventAt;
    public DateTime LastUserEventAt => _lastUserEventAt;
    public DateTime LastTickAt => _lastTickAt;
    public DateTime LastSnapshotAt => _lastSnapshotAt;
    public DateTime LastDriftAt => _lastDriftAt;

    public const int InitialBackoffMs = 1000;
    public const int MaxBackoffMs = 60000;

    // Stage 2b — position events are authoritative. Poll is reduced from 500ms
    // to 60s as a reconciliation safety net. Empirically verified via
    // /api/exposure/diagnostics: 500ms poll = 4459 getPositions calls/min vs
    // 60s poll = 58 calls/min (-98.7%) against 40 logins, 3-min samples.
    private const int PositionSnapshotIntervalMs = 60_000;

    // User balance/credit changes push live through CIMTUserSink, so the bulk
    // account sync is only needed as a reconciliation tick for the roster
    // (group, leverage, comment) and for equity/margin which CIMTUserSink
    // does not carry. 15 min is enough for those fields; the Equity P&L tab
    // has a live MT5 endpoint (/api/equity-pnl/account-live) if the dealer
    // needs fresher equity than the last sync.
    private const int AccountSyncIntervalMinutes = 15;

    /// <summary>
    /// Exponential-backoff progression used by the reconnect loop. Exposed public
    /// so tests can lock the 1s → 60s sequence without spinning up a real MT5
    /// session. Doubles on every failure, capped at <see cref="MaxBackoffMs"/>.
    /// </summary>
    public static int NextBackoffMs(int currentMs) =>
        Math.Min(Math.Max(currentMs, 1) * 2, MaxBackoffMs);

    public bool IsConnected => _api?.IsConnected ?? false;
    public string? ConnectedServer { get; private set; }
    public DateTime? ConnectedAt { get; private set; }
    public int PositionCount { get; private set; }
    public int LoginCount { get; private set; }
    public ulong[] Logins => _logins;

    /// <summary>
    /// Snapshot of native MT5 API call counts since the current connection was
    /// established. Returns zeros if not connected. Combine with
    /// <see cref="ConnectedAt"/> to compute per-minute call rates for the
    /// /api/exposure/diagnostics endpoint.
    /// </summary>
    public IReadOnlyDictionary<string, long> GetApiCallCounts()
    {
        var api = _api;
        if (api is null)
            return new Dictionary<string, long>();
        return new Dictionary<string, long>
        {
            ["getPositions"] = api.GetPositionsCalls,
            ["getUserAccount"] = api.GetUserAccountCalls,
            ["getUserLogins"] = api.GetUserLoginsCalls,
            ["requestDeals"] = api.RequestDealsCalls,
            ["tickLast"] = api.TickLastCalls,
        };
    }

    public MT5ManagerConnection(
        ILogger<MT5ManagerConnection> logger,
        PositionManager positionManager,
        PriceCache priceCache,
        DealStore dealStore,
        Func<Task<List<AccountSettings>>> getAccounts,
        Action onUpdate,
        Func<IEnumerable<TradingAccount>, Task>? syncAccounts = null,
        Func<string, Task<DateTime?>>? getLastDealTime = null,
        Action<string>? onPriceTick = null,
        Action<ClosedDeal>? onDealSettled = null)
    {
        _logger = logger;
        _positionManager = positionManager;
        _priceCache = priceCache;
        _dealStore = dealStore;
        _getAccounts = getAccounts;
        _onUpdate = onUpdate;
        _onPriceTick = onPriceTick;
        _onDealSettled = onDealSettled;
        _syncAccounts = syncAccounts;
        _getLastDealTime = getLastDealTime;
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
                ConnectedAt = DateTime.UtcNow;
                _logger.LogInformation("Connected to MT5 {Server} as login {Login}",
                    managerAccount.Server, managerAccount.Login);

                // Select all symbols for tick streaming (symbol list — not subscription yet)
                if (!_api.SelectedAddAll())
                    _logger.LogWarning("SelectedAddAll failed: {Error}", _api.LastError);

                // ORDERING INVARIANT (don't change without reading this first):
                //   1. GetUserLogins  — need the login array before any per-login query
                //   2. SnapshotPositions — populates B-Book positions in PositionManager
                //   3. BackfillDeals — populates DealStore with historical closes
                //   4. Subscribe to ticks + deals — LAST, so the first live callback fires
                //      against a fully populated cache
                //
                // Older versions subscribed to OnDealAdd BEFORE steps 1–3, which opened
                // a race: deals arriving in the gap triggered broadcasts while
                // PositionManager and DealStore were still empty, producing flickers of
                // stale exposure on the WS hot path and duplicate upserts on the Supa
                // side. The subscribe-last order eliminates both.

                // Step 1 — get logins matching group mask
                var logins = _api.GetUserLogins(managerAccount.GroupMask);
                LoginCount = logins.Length;
                _logger.LogInformation("Found {Count} logins matching group mask '{Mask}'",
                    logins.Length, managerAccount.GroupMask);

                // If no logins from group mask, use the manager login itself
                if (logins.Length == 0)
                    logins = [(ulong)managerAccount.Login];

                _logins = logins;

                // Step 2 — snapshot B-Book positions so the exposure engine has a cache
                // to compute against when the first live tick/deal lands
                SnapshotPositions(logins);

                // Step 3 — backfill closed deals, detect gap from last Supa sync
                var backfillFrom = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
                if (_getLastDealTime != null)
                {
                    try
                    {
                        var lastDealTime = await _getLastDealTime("bbook");
                        if (lastDealTime.HasValue && lastDealTime.Value < DateTime.UtcNow.Date)
                        {
                            backfillFrom = new DateTimeOffset(lastDealTime.Value, TimeSpan.Zero);
                            _logger.LogInformation(
                                "Gap detected: last deal in Supabase at {LastDeal}, backfilling from there instead of today",
                                lastDealTime.Value);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to query last deal time, falling back to today");
                    }
                }
                BackfillDeals(logins, backfillFrom, DateTimeOffset.UtcNow);

                // Initial account sync to Supabase
                await SyncAccountsToSupabaseAsync(logins, "bbook");

                // Step 4 — NOW subscribe to live streams. Backfill window is
                // [backfillFrom, UtcNow-pre-subscribe); callbacks cover [subscribe-time, ∞).
                // Deals that fall in the overlapping millisecond are deduped by DealId
                // on DealStore.AddDeal (ConcurrentDictionary indexer = replace).
                _api.OnTick += OnTickReceived;
                if (!_api.SubscribeTicks())
                    _logger.LogWarning("Tick subscribe failed: {Error}", _api.LastError);
                else
                    _logger.LogInformation("Subscribed to tick stream");

                _api.OnDealAdd += OnDealReceived;
                if (!_api.SubscribeDeals())
                    _logger.LogWarning("Deal subscribe failed: {Error}", _api.LastError);
                else
                    _logger.LogInformation("Subscribed to deal stream");

                // Events are authoritative for PositionManager (Stage 2b);
                // the SnapshotPositions poll below runs every 60s as a
                // reconciliation safety net and reports drift.
                _api.OnPositionAdd += OnPositionAddEvent;
                _api.OnPositionUpdate += OnPositionUpdateEvent;
                _api.OnPositionDelete += OnPositionDeleteEvent;
                if (!_api.SubscribePositions())
                    _logger.LogWarning("Position subscribe failed: {Error}", _api.LastError);
                else
                    _logger.LogInformation("Subscribed to position stream (authoritative)");

                _api.OnUserUpdate += OnUserUpdateEvent;
                if (!_api.SubscribeUsers())
                    _logger.LogWarning("User subscribe failed: {Error}", _api.LastError);
                else
                    _logger.LogInformation("Subscribed to user stream (authoritative)");

                backoffMs = InitialBackoffMs;

                // Position snapshot loop
                while (!stoppingToken.IsCancellationRequested && _api.IsConnected)
                {
                    SnapshotPositions(logins);

                    // Periodic account re-sync + refresh login list for new accounts
                    if ((DateTime.UtcNow - _lastAccountSync).TotalMinutes >= AccountSyncIntervalMinutes)
                    {
                        var freshLogins = _api.GetUserLogins(managerAccount.GroupMask);
                        if (freshLogins.Length > 0 && freshLogins.Length != logins.Length)
                        {
                            _logger.LogInformation("Login list updated: {Old} → {New} logins",
                                logins.Length, freshLogins.Length);
                            logins = freshLogins;
                            _logins = logins;
                            LoginCount = logins.Length;
                        }

                        await SyncAccountsToSupabaseAsync(logins, "bbook");
                    }

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
                    _api.OnPositionAdd -= OnPositionAddEvent;
                    _api.OnPositionUpdate -= OnPositionUpdateEvent;
                    _api.OnPositionDelete -= OnPositionDeleteEvent;
                    _api.OnUserUpdate -= OnUserUpdateEvent;
                    _api.UnsubscribeTicks();
                    _api.UnsubscribeDeals();
                    _api.UnsubscribePositions();
                    _api.UnsubscribeUsers();
                    _api.Disconnect();
                    _api.Dispose();
                    _api = null;
                }
                ConnectedServer = null;
                ConnectedAt = null;
            }
        }

        _logger.LogInformation("MT5 Manager connection service stopped");
    }

    private async Task SyncAccountsToSupabaseAsync(ulong[] logins, string source)
    {
        if (_api == null || !_api.IsConnected || _syncAccounts == null) return;

        try
        {
            var accounts = new List<TradingAccount>();

            foreach (var login in logins)
            {
                var raw = _api.GetUserAccount(login);
                if (raw == null) continue;

                accounts.Add(new TradingAccount
                {
                    Source = source,
                    Login = (long)raw.Login,
                    Name = raw.Name,
                    GroupName = raw.Group,
                    Leverage = (int)raw.Leverage,
                    Balance = raw.Balance,
                    Equity = raw.Equity,
                    Credit = raw.Credit,
                    Margin = raw.Margin,
                    FreeMargin = raw.FreeMargin,
                    Currency = raw.Currency,
                    RegistrationTime = raw.RegistrationTime > 0
                        ? DateTimeOffset.FromUnixTimeSeconds(raw.RegistrationTime).UtcDateTime
                        : null,
                    LastTradeTime = raw.LastTradeTime > 0
                        ? DateTimeOffset.FromUnixTimeSeconds(raw.LastTradeTime).UtcDateTime
                        : null,
                    Comment = raw.Comment,
                    SyncedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            if (accounts.Count > 0)
            {
                await _syncAccounts(accounts);
                _logger.LogInformation("Synced {Count} {Source} trading accounts to Supabase", accounts.Count, source);
            }

            _lastAccountSync = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync accounts to Supabase");
        }
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
                        VolumeLots = pos.Volume,
                        OpenPrice = pos.PriceOpen,
                        CurrentPrice = pos.PriceCurrent,
                        Profit = pos.Profit,
                        Swap = pos.Storage,
                        OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(pos.TimeMsc).UtcDateTime,
                        UpdatedAt = DateTime.UtcNow
                    };
                }
            }

            // Stage 2a drift detection — compute symmetric difference between
            // what events have accumulated in PositionManager and what the MT5
            // poll just returned. A one- or two-position drift per cycle is
            // normal (events racing the poll); persistent larger drift means
            // we missed something and should not flip Stage 2b yet.
            var cachedKeys = new HashSet<string>(_positionManager.GetBBookPositionKeys());
            var snapshotKeys = new HashSet<string>(snapshot.Keys);
            var missingFromCache = snapshotKeys.Count(k => !cachedKeys.Contains(k));
            var extraInCache     = cachedKeys.Count(k => !snapshotKeys.Contains(k));
            var drift = missingFromCache + extraInCache;
            if (drift > 0)
            {
                Interlocked.Add(ref _driftCount, drift);
                Interlocked.Increment(ref _driftPollCount);
                _lastDriftAt = DateTime.UtcNow;
                _logger.LogWarning(
                    "Drift detected: missing-from-cache={Missing} extra-in-cache={Extra} cached={Cached} snapshot={Snapshot}",
                    missingFromCache, extraInCache, cachedKeys.Count, snapshot.Count);
            }

            _positionManager.SnapshotBBookPositions(snapshot);
            PositionCount = snapshot.Count;
            Interlocked.Increment(ref _snapshotCount);
            _lastSnapshotAt = DateTime.UtcNow;
            _onUpdate();

            _logger.LogDebug("Snapshot: {Count} B-Book positions across {Logins} logins",
                snapshot.Count, logins.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to snapshot positions");
        }
    }

    // Stage 2a — events are now authoritative for PositionManager. The 500 ms
    // poll still runs (see SnapshotPositions) and reports drift if the event
    // cache diverges from the poll snapshot. Stage 2b drops the poll interval
    // once drift stays near zero over 24h.
    private static Position ToPosition(RawPosition pos) => new()
    {
        Source = "bbook",
        Login = pos.Login,
        Symbol = pos.Symbol,
        Direction = pos.Action == 0 ? "BUY" : "SELL",
        VolumeLots = pos.Volume,
        OpenPrice = pos.PriceOpen,
        CurrentPrice = pos.PriceCurrent,
        Profit = pos.Profit,
        Swap = pos.Storage,
        OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(pos.TimeMsc).UtcDateTime,
        UpdatedAt = DateTime.UtcNow
    };

    private static string PositionKey(RawPosition pos) =>
        $"bbook:{pos.Login}:{pos.PositionId}";

    private void OnPositionAddEvent(RawPosition pos)
    {
        Interlocked.Increment(ref _positionAddCount);
        _lastPositionEventAt = DateTime.UtcNow;
        _positionManager.UpdateBBookPosition(PositionKey(pos), ToPosition(pos));
        PositionCount = _positionManager.GetBBookPositions().Count;
        _onUpdate();
        _logger.LogDebug("Position add: #{PositionId} {Symbol} login={Login}",
            pos.PositionId, pos.Symbol, pos.Login);
    }

    private void OnPositionUpdateEvent(RawPosition pos)
    {
        Interlocked.Increment(ref _positionUpdateCount);
        _lastPositionEventAt = DateTime.UtcNow;
        _positionManager.UpdateBBookPosition(PositionKey(pos), ToPosition(pos));
        _onUpdate();
    }

    private void OnPositionDeleteEvent(RawPosition pos)
    {
        Interlocked.Increment(ref _positionDeleteCount);
        _lastPositionEventAt = DateTime.UtcNow;
        _positionManager.RemoveBBookPosition(PositionKey(pos));
        PositionCount = _positionManager.GetBBookPositions().Count;
        _onUpdate();
        _logger.LogDebug("Position delete: #{PositionId} login={Login}",
            pos.PositionId, pos.Login);
    }

    private void OnUserUpdateEvent(RawAccount account)
    {
        Interlocked.Increment(ref _userUpdateCount);
        _lastUserEventAt = DateTime.UtcNow;
        _logger.LogDebug("User update: login={Login} balance={Balance} credit={Credit}",
            account.Login, account.Balance, account.Credit);
    }

    private void OnTickReceived(RawTick raw)
    {
        var count = Interlocked.Increment(ref _tickCount);
        _lastTickAt = DateTime.UtcNow;
        if (count <= 5 || count % 10000 == 0)
        {
            _logger.LogInformation("Tick #{Count}: {Symbol} bid={Bid} ask={Ask}",
                count, raw.Symbol, raw.Bid, raw.Ask);
        }

        _priceCache.Update(raw.Symbol, raw.Bid, raw.Ask);

        // Fast-path: ticks arrive at high frequency for active symbols. Routing
        // them through the heavy MarkDirty/full-state broadcast means each tick
        // pays the cost of recomputing exposure across every position. Instead,
        // poke the lightweight price-only broadcaster (when present) and only
        // mark the full state dirty if no price-only fast path is wired.
        if (_onPriceTick is not null)
            _onPriceTick(raw.Symbol);
        else
            _onUpdate();
    }

    private void OnDealReceived(RawDeal raw)
    {
        // All action codes land in DealStore; downstream aggregators
        // (DealStore.GetPnLBySymbol, GetPnLByDay) filter to action < 2 so
        // balance/credit/correction deals don't pollute trade P&L. We need
        // them persisted so the Equity P&L tab can source Net Dep/W + Net
        // Cred + Adj columns from Supabase without hitting MT5 per-request.
        var deal = ConvertDeal(raw);
        _dealStore.AddDeal(deal);
        _onUpdate(); // Trigger WebSocket push so closed row updates in real-time

        // Phase 2.19: fast-path settled-delta push. Lets the Net P&L tab's
        // SETTLED column tick within ~50 ms of a deal close instead of waiting
        // for the next 30 s REST poll + DataSyncService's 30 s Supabase write
        // cycle (worst-case 60 s lag). Skip BALANCE/CREDIT/CORRECTION admin
        // deals (action >= 2) — they don't contribute to settled trade P&L.
        // Frontend overlay clears on the next REST refresh so deltas don't
        // double-count once Supabase catches up.
        if (deal.Action <= 1)
            _onDealSettled?.Invoke(deal);

        _logger.LogDebug("Deal received: #{DealId} Action={Action} {Symbol} {Entry} P&L={Profit}",
            raw.DealId, raw.Action, raw.Symbol, raw.Entry, raw.Profit);
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

    /// <summary>
    /// Read-only query of MT5 deals for verification. Does NOT modify DealStore.
    /// Processes logins in batches to avoid overloading MT5 Manager.
    /// </summary>
    public List<ClosedDeal> QueryDeals(DateTimeOffset from, DateTimeOffset to, int batchSize = 1000)
    {
        if (_api == null || !_api.IsConnected || _logins.Length == 0)
            return [];

        var result = new List<ClosedDeal>();
        var totalBatches = (_logins.Length + batchSize - 1) / batchSize;

        for (int b = 0; b < totalBatches; b++)
        {
            var batch = _logins.Skip(b * batchSize).Take(batchSize).ToArray();
            var batchDeals = 0;

            foreach (var login in batch)
            {
                try
                {
                    var deals = _api.RequestDeals(login, from, to);
                    foreach (var raw in deals)
                    {
                        if (raw.Action >= 2) continue;
                        result.Add(ConvertDeal(raw));
                        batchDeals++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "QueryDeals failed for login {Login}", login);
                }
            }

            _logger.LogInformation("Verify batch {Batch}/{Total}: {Logins} logins, {Deals} deals",
                b + 1, totalBatches, batch.Length, batchDeals);
        }

        _logger.LogInformation("QueryDeals complete: {Total} deals from {Logins} logins ({From:yyyy-MM-dd} to {To:yyyy-MM-dd})",
            result.Count, _logins.Length, from, to);

        return result;
    }

    /// <summary>
    /// Query deals for a single login. Read-only, does not modify DealStore.
    /// </summary>
    public List<ClosedDeal> QueryDealsForLogin(ulong login, DateTimeOffset from, DateTimeOffset to)
    {
        if (_api == null || !_api.IsConnected) return [];
        try
        {
            var deals = _api.RequestDeals(login, from, to);
            return deals
                .Where(raw => raw.Action < 2)
                .Select(raw => ConvertDeal(raw))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QueryDealsForLogin failed for {Login}", login);
            return [];
        }
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
                    // No filter: balance/credit/correction deals (Action>=2) are
                    // kept so they flow into Supabase via DataSyncService and
                    // power the Equity P&L tab's Net Dep/W + Net Cred + Adj
                    // columns. Trade-only consumers filter via DealStore.GetPnL*.
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
        // For IN deals (Entry=0): Action directly maps to direction (BUY/SELL)
        // For OUT deals (Entry=1,3): Action is the CLOSING side, so we keep it as-is
        //   because the deal's Action represents what happened (bought to close / sold to close)
        //   MT5 Manager shows the deal action, not the original position direction
        var direction = raw.Action == 0 ? "BUY" : "SELL";

        return new ClosedDeal
        {
            DealId = raw.DealId,
            Login = raw.Login,
            Symbol = raw.Symbol,
            Direction = direction,
            VolumeLots = raw.VolumeLots,
            Price = raw.Price,
            Profit = raw.Profit,
            Commission = raw.Commission,
            Swap = raw.Storage,
            Fee = raw.Fee,
            Entry = raw.Entry,
            OrderId = raw.OrderId,
            PositionId = raw.PositionId,
            Time = DateTimeOffset.FromUnixTimeMilliseconds(raw.TimeMsc).UtcDateTime,
            Action = raw.Action,
        };
    }

    /// <summary>
    /// Read a single account's live state directly from MT5 Manager, bypassing
    /// the usual 5-min sync cycle. Used for diagnostics / one-shot updates on
    /// accounts that aren't in the `GetUserLogins('*')` pool (e.g. HR
    /// sub-accounts). Returns null when MT5 isn't connected or the login
    /// doesn't exist.
    /// </summary>
    public RawAccount? QueryUserAccount(ulong login)
    {
        if (_api == null || !_api.IsConnected) return null;
        try
        {
            return _api.GetUserAccount(login);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QueryUserAccount failed for {Login}", login);
            return null;
        }
    }

    /// <summary>
    /// Query ALL deals (trade + balance + credit + correction + commission etc.)
    /// for a single login in a date range. Read-only — does NOT touch DealStore.
    /// Used by EquityPnLEngine to classify non-trade deals (actions &gt;= 2) into
    /// the Net Dep/W, Net Cred, and Adjustment columns.
    /// </summary>
    public List<ClosedDeal> QueryAllDealsForLogin(ulong login, DateTimeOffset from, DateTimeOffset to)
    {
        if (_api == null || !_api.IsConnected) return [];
        try
        {
            return _api.RequestDeals(login, from, to)
                .Select(raw => ConvertDeal(raw))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QueryAllDealsForLogin failed for {Login}", login);
            return [];
        }
    }
}
