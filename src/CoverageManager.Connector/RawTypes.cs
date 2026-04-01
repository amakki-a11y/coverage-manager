namespace CoverageManager.Connector;

/// <summary>
/// Raw deal data copied from CIMTDeal inside the callback.
/// Every field captured immediately — native object invalid after callback returns.
/// </summary>
public sealed record RawDeal
{
    public required ulong DealId { get; init; }
    public required ulong Login { get; init; }
    public required long TimeMsc { get; init; }
    public required string Symbol { get; init; }
    public required uint Action { get; init; }   // 0=BUY, 1=SELL, 2=BALANCE
    public required ulong VolumeRaw { get; init; }
    public required double Price { get; init; }
    public required double Profit { get; init; }
    public required double Commission { get; init; }
    public required double Storage { get; init; } // Swap
    public required double Fee { get; init; }
    public required uint Entry { get; init; }     // 0=IN, 1=OUT, 2=INOUT, 3=OUT_BY
    public required ulong PositionId { get; init; }
    public required string Comment { get; init; }
    public double VolumeLots => VolumeRaw / 10000.0;
}

/// <summary>
/// Raw open position data from MT5 PositionGet.
/// Plain C# record — no MetaQuotes dependencies.
/// </summary>
public sealed record RawPosition
{
    public required ulong PositionId { get; init; }
    public required ulong Login { get; init; }
    public required string Symbol { get; init; }
    public required uint Action { get; init; }   // 0=BUY, 1=SELL
    public required double Volume { get; init; }  // Standard lots
    public required double PriceOpen { get; init; }
    public required double PriceCurrent { get; init; }
    public required double Profit { get; init; }
    public required double Storage { get; init; } // Swap
    public required long TimeMsc { get; init; }
}

/// <summary>
/// Raw tick data from the MT5 tick callback.
/// </summary>
public sealed record RawTick
{
    public required string Symbol { get; init; }
    public required double Bid { get; init; }
    public required double Ask { get; init; }
    public required long TimeMsc { get; init; }
}
