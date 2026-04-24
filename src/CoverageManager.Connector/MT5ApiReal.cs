namespace CoverageManager.Connector;

#if MT5_API_AVAILABLE

using MetaQuotes.MT5CommonAPI;
using MetaQuotes.MT5ManagerAPI;

/// <summary>
/// Real IMT5Api wrapping the native MetaQuotes Manager API DLLs.
/// Behind #if MT5_API_AVAILABLE so the solution builds without native DLLs.
/// </summary>
public sealed class MT5ApiReal : IMT5Api
{
    // Shared factory initialization — MT5 API can only be initialized once per process
    private static readonly object _factoryLock = new();
    private static int _instanceCount;
    private static bool _factoryInitialized;

    private CIMTManagerAPI? _manager;
    private CIMTDealArray? _dealArray;
    private TickSinkHandler? _tickSink;
    private DealSinkHandler? _dealSink;
    private PositionSinkHandler? _positionSink;
    private UserSinkHandler? _userSink;
    private bool _connected;
    private bool _disposed;

    public event Action<RawTick>? OnTick;
    public event Action<RawDeal>? OnDealAdd;
    public event Action<RawPosition>? OnPositionAdd;
    public event Action<RawPosition>? OnPositionUpdate;
    public event Action<ulong>? OnPositionDelete;
    public event Action<RawAccount>? OnUserUpdate;
    public bool IsConnected => _connected && _manager != null;
    public string LastError { get; private set; } = "";

    public bool Initialize()
    {
        lock (_factoryLock)
        {
            if (!_factoryInitialized)
            {
                var initRes = SMTManagerAPIFactory.Initialize(null);
                if (initRes != MTRetCode.MT_RET_OK)
                {
                    LastError = $"MT5 API initialization failed ({initRes})";
                    return false;
                }
                _factoryInitialized = true;
            }
            _instanceCount++;
        }

        var res = MTRetCode.MT_RET_OK;
        _manager = SMTManagerAPIFactory.CreateManager(
            SMTManagerAPIFactory.ManagerAPIVersion, out res);

        if (res != MTRetCode.MT_RET_OK || _manager == null)
        {
            LastError = $"Creating manager interface failed ({res})";
            return false;
        }

        _dealArray = _manager.DealCreateArray();
        return true;
    }

    public bool Connect(string server, ulong login, string password, uint timeoutMs = 30000)
    {
        if (_manager == null)
        {
            LastError = "Manager not initialized — call Initialize() first";
            return false;
        }

        var res = _manager.Connect(server, login, password, null,
            CIMTManagerAPI.EnPumpModes.PUMP_MODE_FULL, timeoutMs);

        _connected = res == MTRetCode.MT_RET_OK;
        if (!_connected)
            LastError = $"Connection failed ({res})";
        return _connected;
    }

    public void Disconnect()
    {
        _connected = false;
        UnsubscribeDeals();
        UnsubscribeTicks();
        UnsubscribePositions();
        UnsubscribeUsers();
        _manager?.Disconnect();
    }

    public bool SubscribeDeals()
    {
        if (_manager == null) { LastError = "Manager is null"; return false; }

        _dealSink = new DealSinkHandler(this);
        var regRes = _dealSink.RegisterSink();
        if (regRes != MTRetCode.MT_RET_OK)
        {
            LastError = $"DealSink.RegisterSink failed: {regRes}";
            return false;
        }

        var res = _manager.DealSubscribe(_dealSink);
        if (res != MTRetCode.MT_RET_OK)
            LastError = $"DealSubscribe failed: {res}";
        return res == MTRetCode.MT_RET_OK;
    }

    public void UnsubscribeDeals()
    {
        if (_manager != null && _dealSink != null)
        {
            _manager.DealUnsubscribe(_dealSink);
            _dealSink = null;
        }
    }

    public List<RawDeal> RequestDeals(ulong login, DateTimeOffset from, DateTimeOffset to)
    {
        var result = new List<RawDeal>();
        if (_manager == null || _dealArray == null) return result;

        _dealArray.Clear();
        var fromMt5 = SMTTime.FromDateTime(from.UtcDateTime);
        var toMt5 = SMTTime.FromDateTime(to.UtcDateTime);

        var res = _manager.DealRequest(login, fromMt5, toMt5, _dealArray);
        if (res != MTRetCode.MT_RET_OK) return result;

        for (uint i = 0; i < _dealArray.Total(); i++)
        {
            var deal = _dealArray.Next(i);
            if (deal == null) continue;
            result.Add(CopyDeal(deal));
        }

        return result;
    }

    private static RawDeal CopyDeal(CIMTDeal deal)
    {
        // Native API returns double; cast to decimal at the boundary so the rest of
        // the system operates on exact money types. Single place the cast happens.
        return new RawDeal
        {
            DealId = deal.Deal(),
            Login = deal.Login(),
            TimeMsc = deal.TimeMsc(),
            Symbol = deal.Symbol(),
            Action = deal.Action(),
            VolumeRaw = (ulong)deal.Volume(),
            Price = (decimal)deal.Price(),
            Profit = (decimal)deal.Profit(),
            Commission = (decimal)deal.Commission(),
            Storage = (decimal)deal.Storage(),
            Fee = (decimal)deal.Fee(),
            Entry = deal.Entry(),
            OrderId = deal.Order(),
            PositionId = (ulong)deal.PositionID(),
            Comment = deal.Comment()
        };
    }

    public bool SubscribeTicks(string symbolMask = "*")
    {
        if (_manager == null) { LastError = "Manager is null"; return false; }

        _tickSink = new TickSinkHandler(this);

        var regRes = _tickSink.RegisterSink();
        if (regRes != MTRetCode.MT_RET_OK)
        {
            LastError = $"TickSink.RegisterSink failed: {regRes}";
            return false;
        }

        var res = _manager.TickSubscribe(_tickSink);
        if (res != MTRetCode.MT_RET_OK)
            LastError = $"TickSubscribe failed: {res}";
        return res == MTRetCode.MT_RET_OK;
    }

    public void UnsubscribeTicks()
    {
        if (_manager != null && _tickSink != null)
        {
            _manager.TickUnsubscribe(_tickSink);
            _tickSink = null;
        }
    }

    public bool SubscribePositions()
    {
        if (_manager == null) { LastError = "Manager is null"; return false; }

        _positionSink = new PositionSinkHandler(this);
        var regRes = _positionSink.RegisterSink();
        if (regRes != MTRetCode.MT_RET_OK)
        {
            LastError = $"PositionSink.RegisterSink failed: {regRes}";
            return false;
        }

        var res = _manager.PositionSubscribe(_positionSink);
        if (res != MTRetCode.MT_RET_OK)
            LastError = $"PositionSubscribe failed: {res}";
        return res == MTRetCode.MT_RET_OK;
    }

    public void UnsubscribePositions()
    {
        if (_manager != null && _positionSink != null)
        {
            _manager.PositionUnsubscribe(_positionSink);
            _positionSink = null;
        }
    }

    public bool SubscribeUsers()
    {
        if (_manager == null) { LastError = "Manager is null"; return false; }

        _userSink = new UserSinkHandler(this);
        var regRes = _userSink.RegisterSink();
        if (regRes != MTRetCode.MT_RET_OK)
        {
            LastError = $"UserSink.RegisterSink failed: {regRes}";
            return false;
        }

        var res = _manager.UserSubscribe(_userSink);
        if (res != MTRetCode.MT_RET_OK)
            LastError = $"UserSubscribe failed: {res}";
        return res == MTRetCode.MT_RET_OK;
    }

    public void UnsubscribeUsers()
    {
        if (_manager != null && _userSink != null)
        {
            _manager.UserUnsubscribe(_userSink);
            _userSink = null;
        }
    }

    public bool SelectedAddAll()
    {
        if (_manager == null) { LastError = "Manager is null"; return false; }
        var res = _manager.SelectedAddAll();
        if (res != MTRetCode.MT_RET_OK)
        {
            LastError = $"SelectedAddAll failed: {res}";
            return false;
        }
        return true;
    }

    public List<RawPosition> GetPositions(ulong login)
    {
        var result = new List<RawPosition>();
        if (_manager == null) return result;

        var posArray = _manager.PositionCreateArray();
        if (posArray == null) return result;

        var res = _manager.PositionGet(login, posArray);
        if (res != MTRetCode.MT_RET_OK)
        {
            posArray.Dispose();
            return result;
        }

        for (uint i = 0; i < posArray.Total(); i++)
        {
            var pos = posArray.Next(i);
            if (pos == null) continue;

            result.Add(new RawPosition
            {
                PositionId = pos.Position(),
                Login = pos.Login(),
                Symbol = pos.Symbol(),
                Action = pos.Action(),
                Volume = (decimal)pos.Volume() / 10000m,
                PriceOpen = (decimal)pos.PriceOpen(),
                PriceCurrent = (decimal)pos.PriceCurrent(),
                Profit = (decimal)pos.Profit(),
                Storage = (decimal)pos.Storage(),
                TimeMsc = (long)pos.TimeCreate() * 1000
            });
        }

        posArray.Dispose();
        return result;
    }

    public ulong[] GetUserLogins(string groupMask)
    {
        if (_manager == null) return Array.Empty<ulong>();
        var logins = _manager.UserLogins(groupMask, out var res);
        return res == MTRetCode.MT_RET_OK && logins != null ? logins : Array.Empty<ulong>();
    }

    public RawTick? GetTickLast(string symbol)
    {
        if (_manager == null) return null;
        try
        {
            var res = _manager.TickLast(symbol, out MTTickShort tick);
            if (res != MTRetCode.MT_RET_OK) return null;
            if (tick.bid <= 0 && tick.ask <= 0) return null;
            return new RawTick
            {
                Symbol = symbol,
                Bid = (decimal)tick.bid,
                Ask = (decimal)tick.ask,
                TimeMsc = tick.datetime_msc
            };
        }
        catch { return null; }
    }

    public RawAccount? GetUserAccount(ulong login)
    {
        if (_manager == null) return null;
        try
        {
            // Get user info (name, group, leverage, balance, etc.)
            var user = _manager.UserCreate();
            if (user == null) return null;

            var res = _manager.UserGet(login, user);
            if (res != MTRetCode.MT_RET_OK)
            {
                user.Dispose();
                return null;
            }

            // Get live account data (equity, margin, free margin)
            double equity = 0, margin = 0, freeMargin = 0;
            var acct = _manager.UserCreateAccount();
            if (acct != null)
            {
                var acctRes = _manager.UserAccountGet(login, acct);
                if (acctRes == MTRetCode.MT_RET_OK)
                {
                    equity = acct.Equity();
                    margin = acct.Margin();
                    freeMargin = acct.MarginFree();
                }
                acct.Dispose();
            }

            // Currency comes from group settings — default to USD
            string currency = "USD";

            var account = new RawAccount
            {
                Login = user.Login(),
                Name = user.Name(),
                Group = user.Group(),
                Leverage = user.Leverage(),
                Balance = (decimal)user.Balance(),
                Equity = (decimal)equity,
                // Credit bucket is separate from Balance in MT5. Admin-level
                // credit-to-balance transfers move value between these two
                // without emitting a deal record — tracking Credit is how the
                // Equity P&L tab reconciles Net Credit from Current − Begin.
                Credit = (decimal)user.Credit(),
                Margin = (decimal)margin,
                FreeMargin = (decimal)freeMargin,
                Currency = currency,
                RegistrationTime = (long)user.Registration(),
                LastTradeTime = (long)user.LastAccess(),
                Comment = user.Comment(),
                BalancePrevDay = 0m,
                EquityPrevDay = 0m
            };

            user.Dispose();
            return account;
        }
        catch { return null; }
    }

    internal void FireTick(RawTick tick) => OnTick?.Invoke(tick);
    internal void FireDealAdd(RawDeal deal) => OnDealAdd?.Invoke(deal);
    internal void FirePositionAdd(RawPosition pos) => OnPositionAdd?.Invoke(pos);
    internal void FirePositionUpdate(RawPosition pos) => OnPositionUpdate?.Invoke(pos);
    internal void FirePositionDelete(ulong positionId) => OnPositionDelete?.Invoke(positionId);
    internal void FireUserUpdate(RawAccount account) => OnUserUpdate?.Invoke(account);

    private static RawPosition CopyPosition(CIMTPosition pos) => new()
    {
        PositionId = pos.Position(),
        Login = pos.Login(),
        Symbol = pos.Symbol(),
        Action = pos.Action(),
        Volume = (decimal)pos.Volume() / 10000m,
        PriceOpen = (decimal)pos.PriceOpen(),
        PriceCurrent = (decimal)pos.PriceCurrent(),
        Profit = (decimal)pos.Profit(),
        Storage = (decimal)pos.Storage(),
        TimeMsc = (long)pos.TimeCreate() * 1000
    };

    // Shadow-mode user-event copy. Equity/Margin come from a separate IMTAccount
    // fetch (UserAccountGet) which we skip on the event path to avoid amplifying
    // load — Stage 2 will decide whether to enrich on a subset of fields.
    private static RawAccount CopyUserMinimal(CIMTUser user) => new()
    {
        Login = user.Login(),
        Name = user.Name(),
        Group = user.Group(),
        Leverage = user.Leverage(),
        Balance = (decimal)user.Balance(),
        Equity = 0m,
        Credit = (decimal)user.Credit(),
        Margin = 0m,
        FreeMargin = 0m,
        Currency = "USD",
        RegistrationTime = (long)user.Registration(),
        LastTradeTime = (long)user.LastAccess(),
        Comment = user.Comment(),
        BalancePrevDay = 0m,
        EquityPrevDay = 0m
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
        _dealArray?.Dispose();
        _dealSink?.Dispose();
        _tickSink?.Dispose();
        _positionSink?.Dispose();
        _userSink?.Dispose();
        _manager?.Dispose();
        _manager = null;

        lock (_factoryLock)
        {
            _instanceCount--;
            if (_instanceCount <= 0 && _factoryInitialized)
            {
                SMTManagerAPIFactory.Shutdown();
                _factoryInitialized = false;
            }
        }
    }

    private sealed class DealSinkHandler : CIMTDealSink
    {
        private readonly MT5ApiReal _owner;
        public DealSinkHandler(MT5ApiReal owner) => _owner = owner;

        public override void OnDealAdd(CIMTDeal deal)
        {
            var raw = CopyDeal(deal);
            _owner.FireDealAdd(raw);
        }

        public override void OnDealUpdate(CIMTDeal deal)
        {
            // Deal updates also forwarded as adds (same P&L tracking path)
            var raw = CopyDeal(deal);
            _owner.FireDealAdd(raw);
        }
    }

    /// <summary>
    /// Tick sink handler — copies native tick data and fires OnTick events.
    /// Both overloads covered for different MT5 server builds.
    /// </summary>
    private sealed class TickSinkHandler : CIMTTickSink
    {
        private readonly MT5ApiReal _owner;
        public TickSinkHandler(MT5ApiReal owner) => _owner = owner;

        public override void OnTick(string symbol, MTTickShort tick)
        {
            _owner.FireTick(new RawTick
            {
                Symbol = symbol,
                Bid = (decimal)tick.bid,
                Ask = (decimal)tick.ask,
                TimeMsc = tick.datetime_msc
            });
        }

        public override void OnTick(int feeder, MTTick tick)
        {
            if (tick.bid > 0 || tick.ask > 0)
            {
                _owner.FireTick(new RawTick
                {
                    Symbol = tick.symbol,
                    Bid = (decimal)tick.bid,
                    Ask = (decimal)tick.ask,
                    TimeMsc = tick.datetime_msc
                });
            }
        }
    }

    /// <summary>
    /// Position sink — fires OnPositionAdd/Update/Delete when the MT5 server
    /// pushes position state changes. Shadow-mode in Stage 1: consumers only
    /// count events; the 500ms snapshot poll still owns PositionManager.
    /// </summary>
    private sealed class PositionSinkHandler : CIMTPositionSink
    {
        private readonly MT5ApiReal _owner;
        public PositionSinkHandler(MT5ApiReal owner) => _owner = owner;

        public override void OnPositionAdd(CIMTPosition pos)
            => _owner.FirePositionAdd(CopyPosition(pos));

        public override void OnPositionUpdate(CIMTPosition pos)
            => _owner.FirePositionUpdate(CopyPosition(pos));

        public override void OnPositionDelete(CIMTPosition pos)
            => _owner.FirePositionDelete(pos.Position());
    }

    /// <summary>
    /// User sink — fires OnUserUpdate on balance/credit/group changes. Admin
    /// balance ↔ credit transfers do NOT emit a deal, so this is the only
    /// live signal for Net Credit reconciliation on the Equity P&L tab.
    /// </summary>
    private sealed class UserSinkHandler : CIMTUserSink
    {
        private readonly MT5ApiReal _owner;
        public UserSinkHandler(MT5ApiReal owner) => _owner = owner;

        public override void OnUserUpdate(CIMTUser user)
            => _owner.FireUserUpdate(CopyUserMinimal(user));

        public override void OnUserAdd(CIMTUser user)
            => _owner.FireUserUpdate(CopyUserMinimal(user));
    }
}

#endif
