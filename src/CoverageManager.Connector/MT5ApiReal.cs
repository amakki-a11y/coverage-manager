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
    private bool _connected;
    private bool _disposed;

    public event Action<RawTick>? OnTick;
    public event Action<RawDeal>? OnDealAdd;
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
        return new RawDeal
        {
            DealId = deal.Deal(),
            Login = deal.Login(),
            TimeMsc = deal.TimeMsc(),
            Symbol = deal.Symbol(),
            Action = deal.Action(),
            VolumeRaw = (ulong)deal.Volume(),
            Price = deal.Price(),
            Profit = deal.Profit(),
            Commission = deal.Commission(),
            Storage = deal.Storage(),
            Fee = deal.Fee(),
            Entry = deal.Entry(),
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
                Volume = pos.Volume() / 10000.0,
                PriceOpen = pos.PriceOpen(),
                PriceCurrent = pos.PriceCurrent(),
                Profit = pos.Profit(),
                Storage = pos.Storage(),
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
                Bid = tick.bid,
                Ask = tick.ask,
                TimeMsc = tick.datetime_msc
            };
        }
        catch { return null; }
    }

    internal void FireTick(RawTick tick) => OnTick?.Invoke(tick);
    internal void FireDealAdd(RawDeal deal) => OnDealAdd?.Invoke(deal);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Disconnect();
        _dealArray?.Dispose();
        _dealSink?.Dispose();
        _tickSink?.Dispose();
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
                Bid = tick.bid,
                Ask = tick.ask,
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
                    Bid = tick.bid,
                    Ask = tick.ask,
                    TimeMsc = tick.datetime_msc
                });
            }
        }
    }
}

#endif
