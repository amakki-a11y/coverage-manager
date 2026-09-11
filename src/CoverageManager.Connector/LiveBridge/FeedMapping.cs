using System.Globalization;
using System.Text.Json;

namespace CoverageManager.Connector.LiveBridge;

/// <summary>
/// Feed payloads (the bridge's record shapes, MT5 names in PascalCase) to the connector's Raw* records, with the same
/// units <see cref="MT5ApiReal"/> produces from the Manager API:
///   VolumeExt is lots x 1e8 -> RawPosition.Volume in lots, RawDeal.VolumeRaw in 1/10000 lot;
///   TimeCreate / Time are the source's seconds -> TimeMsc = seconds x 1000 (the Manager API's TimeCreate() x 1000);
///   prices and money are read as decimal straight from the JSON text (no double round trip).
/// Not on the wire and therefore fixed here: deal Fee (0), account RegistrationTime / LastTradeTime (0), Comment ("").
/// An account frame without computed money (Computed = false) gets Equity = Balance + Credit and Margin = 0, which is
/// exact for an account with no open positions and a one-second transient for one that has.
/// </summary>
public static class FeedMapping
{
    /// <summary>The position payload; null when it carries no Position id.</summary>
    public static RawPosition? Position(JsonElement p)
    {
        if (p.ValueKind != JsonValueKind.Object) return null;
        var id = ULong(p, "Position");
        if (id == 0) return null;
        return new RawPosition
        {
            PositionId = id,
            Login = ULong(p, "Login"),
            Symbol = Str(p, "Symbol"),
            Action = UInt(p, "Action"),
            Volume = Math.Max(0, Long(p, "VolumeExt")) / 100_000_000m,
            PriceOpen = Dec(p, "PriceOpen"),
            PriceCurrent = Dec(p, "PriceCurrent"),
            Profit = Dec(p, "Profit"),
            Storage = Dec(p, "Storage"),
            TimeMsc = Long(p, "TimeCreate") * 1000,
        };
    }

    /// <summary>The closing deal a position delete may carry (the bridge's closing deal linker), when present.</summary>
    public static ulong? ClosingDeal(JsonElement p)
    {
        if (p.ValueKind != JsonValueKind.Object) return null;
        var v = ULong(p, "ClosingDeal");
        return v == 0 ? null : v;
    }

    /// <summary>The deal payload; null when it carries no Deal number.</summary>
    public static RawDeal? Deal(JsonElement p)
    {
        if (p.ValueKind != JsonValueKind.Object) return null;
        var id = ULong(p, "Deal");
        if (id == 0) return null;
        var volumeExt = Math.Max(0, Long(p, "VolumeExt"));
        return new RawDeal
        {
            DealId = id,
            Login = ULong(p, "Login"),
            TimeMsc = Long(p, "Time") * 1000,
            Symbol = Str(p, "Symbol"),
            Action = UInt(p, "Action"),
            VolumeRaw = (ulong)((volumeExt + 5_000) / 10_000),   // 1e8 -> 1e4 per lot, rounded
            Price = Dec(p, "Price"),
            Profit = Dec(p, "Profit"),
            Commission = Dec(p, "Commission"),
            Storage = Dec(p, "Storage"),
            Fee = 0m,                                            // not on the wire
            Entry = UInt(p, "Entry"),
            OrderId = ULong(p, "Order"),
            PositionId = ULong(p, "PositionID"),
            Comment = Str(p, "Comment"),
        };
    }

    /// <summary>The account payload; null when it carries no Login.</summary>
    public static RawAccount? Account(JsonElement p)
    {
        if (p.ValueKind != JsonValueKind.Object) return null;
        var login = ULong(p, "Login");
        if (login == 0) return null;
        var balance = Dec(p, "Balance");
        var credit = Dec(p, "Credit");
        var equity = NullableDec(p, "Equity") ?? balance + credit;
        var margin = NullableDec(p, "Margin") ?? 0m;
        var marginFree = NullableDec(p, "MarginFree") ?? equity - margin;
        var currency = Str(p, "Currency");
        return new RawAccount
        {
            Login = login,
            Name = Str(p, "Name"),
            Group = Str(p, "Group"),
            Leverage = UInt(p, "Leverage"),
            Balance = balance,
            Equity = equity,
            Credit = credit,
            Margin = margin,
            FreeMargin = marginFree,
            Currency = currency.Length == 0 ? "USD" : currency,
            RegistrationTime = 0,
            LastTradeTime = 0,
            Comment = "",
            BalancePrevDay = 0m,
            EquityPrevDay = 0m,
        };
    }

    /// <summary>Whether the account frame's money is the bridge's computed figures (true) or only the pushed balance and credit.</summary>
    public static bool AccountComputed(JsonElement p)
        => p.ValueKind == JsonValueKind.Object && p.TryGetProperty("Computed", out var c) && c.ValueKind == JsonValueKind.True;

    /// <summary>The tick payload; null without a symbol or without a price (as the Manager API path drops bid = ask = 0).</summary>
    public static RawTick? Tick(JsonElement p)
    {
        if (p.ValueKind != JsonValueKind.Object) return null;
        var symbol = Str(p, "Symbol");
        if (symbol.Length == 0) return null;
        var bid = Dec(p, "Bid");
        var ask = Dec(p, "Ask");
        if (bid <= 0 && ask <= 0) return null;
        return new RawTick { Symbol = symbol, Bid = bid, Ask = ask, TimeMsc = Long(p, "TimeMsc") };
    }

    // ---- JSON helpers: tolerant of a missing key, a null, or a number sent as a string ----

    private static string Str(JsonElement p, string name)
        => p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static long Long(JsonElement p, string name)
    {
        if (!p.TryGetProperty(name, out var v)) return 0;
        switch (v.ValueKind)
        {
            case JsonValueKind.Number:
                if (v.TryGetInt64(out var l)) return l;
                if (v.TryGetDouble(out var d) && double.IsFinite(d)) return (long)Math.Round(d);
                return 0;
            case JsonValueKind.String:
                return long.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : 0;
            default:
                return 0;
        }
    }

    private static ulong ULong(JsonElement p, string name)
    {
        var l = Long(p, name);
        return l < 0 ? 0 : (ulong)l;
    }

    private static uint UInt(JsonElement p, string name)
    {
        var l = Long(p, name);
        return l < 0 || l > uint.MaxValue ? 0 : (uint)l;
    }

    private static decimal Dec(JsonElement p, string name) => NullableDec(p, name) ?? 0m;

    private static decimal? NullableDec(JsonElement p, string name)
    {
        if (!p.TryGetProperty(name, out var v)) return null;
        switch (v.ValueKind)
        {
            case JsonValueKind.Number:
                if (v.TryGetDecimal(out var d)) return d;
                if (v.TryGetDouble(out var dbl) && double.IsFinite(dbl)) return (decimal)dbl;
                return null;
            case JsonValueKind.String:
                return decimal.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : null;
            default:
                return null;
        }
    }
}
