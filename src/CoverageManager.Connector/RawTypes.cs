namespace CoverageManager.Connector;

/// <summary>
/// Raw deal data copied from CIMTDeal inside the callback.
/// Every field captured immediately — native object invalid after callback returns.
/// Money-bearing fields use <c>decimal</c> per the project-wide rule:
/// <c>double</c> silently loses precision on FX quotes (1.08547 * 100000 = 108547.00000…)
/// and every downstream P&amp;L inherits the drift. The MT5 native API returns
/// <c>double</c>; the cast from double → decimal happens once, at ingest, in
/// <see cref="MT5ApiReal"/>.
/// </summary>
public sealed record RawDeal
{
    public required ulong DealId { get; init; }
    public required ulong Login { get; init; }
    public required long TimeMsc { get; init; }
    public required string Symbol { get; init; }
    public required uint Action { get; init; }   // 0=BUY, 1=SELL, 2=BALANCE
    public required ulong VolumeRaw { get; init; }
    public required decimal Price { get; init; }
    public required decimal Profit { get; init; }
    public required decimal Commission { get; init; }
    public required decimal Storage { get; init; } // Swap
    public required decimal Fee { get; init; }
    public required uint Entry { get; init; }     // 0=IN, 1=OUT, 2=INOUT, 3=OUT_BY
    public required ulong OrderId { get; init; }   // MTDeal.Order — ticket of the order that created this deal
    public required ulong PositionId { get; init; }
    public required string Comment { get; init; }
    public decimal VolumeLots => VolumeRaw / 10000m;
}

/// <summary>
/// Raw open position data from MT5 PositionGet.
/// Plain C# record — no MetaQuotes dependencies. Money-bearing fields use <c>decimal</c>.
/// </summary>
public sealed record RawPosition
{
    public required ulong PositionId { get; init; }
    public required ulong Login { get; init; }
    public required string Symbol { get; init; }
    public required uint Action { get; init; }   // 0=BUY, 1=SELL
    public required decimal Volume { get; init; }  // Standard lots
    public required decimal PriceOpen { get; init; }
    public required decimal PriceCurrent { get; init; }
    public required decimal Profit { get; init; }
    public required decimal Storage { get; init; } // Swap
    public required long TimeMsc { get; init; }
}

/// <summary>
/// Raw tick data from the MT5 tick callback. Bid/Ask are prices → <c>decimal</c>.
/// </summary>
public sealed record RawTick
{
    public required string Symbol { get; init; }
    public required decimal Bid { get; init; }
    public required decimal Ask { get; init; }
    public required long TimeMsc { get; init; }
}

/// <summary>
/// Raw account data from MT5 UserGet.
/// Captures key fields for syncing to Supabase trading_accounts table.
/// Money-bearing fields (Balance, Equity, Margin, …) use <c>decimal</c>.
/// </summary>
public sealed record RawAccount
{
    public required ulong Login { get; init; }
    public required string Name { get; init; }
    public required string Group { get; init; }
    public required uint Leverage { get; init; }
    public required decimal Balance { get; init; }
    public required decimal Equity { get; init; }
    public required decimal Margin { get; init; }
    public required decimal FreeMargin { get; init; }
    public required string Currency { get; init; }
    public required long RegistrationTime { get; init; }
    public required long LastTradeTime { get; init; }
    public required string Comment { get; init; }
    public required decimal BalancePrevDay { get; init; }
    public required decimal EquityPrevDay { get; init; }
}
