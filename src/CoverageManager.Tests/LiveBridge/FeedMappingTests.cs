using System.Text.Json;
using CoverageManager.Connector.LiveBridge;

namespace CoverageManager.Tests.LiveBridge;

/// <summary>Feed payloads to the Raw* records: the units the Manager API path produces, exact decimals, and the fields the wire does not carry.</summary>
[TestClass]
public class FeedMappingTests
{
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [TestMethod]
    public void Position_LotsFromVolumeExt_MillisecondsFromSourceSeconds_ExactPrices()
    {
        var p = FeedMapping.Position(Json(FeedPayloadJson.Position(45369295, 1065, "EURUSD", 1, 0.03m, 1.08547, 1.08512, -10.5, -0.31, 1_789_000_000)))!;
        Assert.AreEqual(45369295UL, p.PositionId);
        Assert.AreEqual(1065UL, p.Login);
        Assert.AreEqual("EURUSD", p.Symbol);
        Assert.AreEqual(1u, p.Action);
        Assert.AreEqual(0.03m, p.Volume);
        Assert.AreEqual(1.08547m, p.PriceOpen);
        Assert.AreEqual(1.08512m, p.PriceCurrent);
        Assert.AreEqual(-10.5m, p.Profit);
        Assert.AreEqual(-0.31m, p.Storage);
        Assert.AreEqual(1_789_000_000_000L, p.TimeMsc);
        Assert.AreEqual(1.08547m, p.PriceOpen, "a decimal read from the JSON text, not a double round trip");

        Assert.IsNull(FeedMapping.Position(Json("{\"Login\":1}")), "no Position id");
        Assert.IsNull(FeedMapping.Position(Json("42")));
        Assert.AreEqual(777UL, FeedMapping.ClosingDeal(Json("{\"Position\":1,\"ClosingDeal\":777}")));
        Assert.IsNull(FeedMapping.ClosingDeal(Json("{\"Position\":1}")));
    }

    [TestMethod]
    public void Deal_VolumeRawInTenThousandthsOfALot_FourMoneyFields_CommentNullBecomesEmpty()
    {
        var d = FeedMapping.Deal(Json(FeedPayloadJson.Deal(90001, 1065, "XAUUSD", 1, 1, 0.25m, 2405.55, 45.25, -0.2, -0.7, 1_789_000_500, 8001, 45369295, fee: -0.05)))!;
        Assert.AreEqual(90001UL, d.DealId);
        Assert.AreEqual(1065UL, d.Login);
        Assert.AreEqual("XAUUSD", d.Symbol);
        Assert.AreEqual(1u, d.Action);
        Assert.AreEqual(1u, d.Entry);
        Assert.AreEqual(2500UL, d.VolumeRaw);
        Assert.AreEqual(0.25m, d.VolumeLots);
        Assert.AreEqual(2405.55m, d.Price);
        Assert.AreEqual(45.25m, d.Profit);
        Assert.AreEqual(-0.2m, d.Storage);
        Assert.AreEqual(-0.7m, d.Commission);
        Assert.AreEqual(-0.05m, d.Fee);
        Assert.AreEqual(1_789_000_500_000L, d.TimeMsc);
        Assert.AreEqual(8001UL, d.OrderId);
        Assert.AreEqual(45369295UL, d.PositionId);
        Assert.AreEqual("", d.Comment);

        var withComment = FeedMapping.Deal(Json(FeedPayloadJson.Deal(1, 1, "X", 0, 0, 1m, 1, 0, 0, 0, 1, 1, 1, "sl 1.0800")))!;
        Assert.AreEqual("sl 1.0800", withComment.Comment);
        Assert.IsNull(FeedMapping.Deal(Json("{\"Login\":1}")));

        // A volume finer than a ten-thousandth of a lot rounds to the nearest (the Raw contract's unit);
        // a payload without Fee (a bridge older than 2026-09-11) reads as 0.
        var fine = FeedMapping.Deal(Json("{\"Deal\":2,\"Login\":1,\"VolumeExt\":123456}"))!;
        Assert.AreEqual(12UL, fine.VolumeRaw);
        Assert.AreEqual(0m, fine.Fee);
        Assert.AreEqual(0m, fine.Commission);
    }

    [TestMethod]
    public void Account_ComputedMoneyIsTaken_UncomputedFallsBackToBalancePlusCredit()
    {
        var computed = FeedMapping.Account(Json(FeedPayloadJson.Account(1065, "real\\A", 100, "USD", 10000, 500, 10552, 52, 120, 10432, true, "Alice")))!;
        Assert.AreEqual(1065UL, computed.Login);
        Assert.AreEqual("real\\A", computed.Group);
        Assert.AreEqual(100u, computed.Leverage);
        Assert.AreEqual("USD", computed.Currency);
        Assert.AreEqual(10000m, computed.Balance);
        Assert.AreEqual(500m, computed.Credit);
        Assert.AreEqual(10552m, computed.Equity);
        Assert.AreEqual(120m, computed.Margin);
        Assert.AreEqual(10432m, computed.FreeMargin);
        Assert.AreEqual("Alice", computed.Name);
        Assert.AreEqual(0L, computed.RegistrationTime);
        Assert.AreEqual(0L, computed.LastTradeTime);
        Assert.AreEqual("", computed.Comment);
        Assert.IsTrue(FeedMapping.AccountComputed(Json(FeedPayloadJson.Account(1, "g", 1, "USD", 1, 0, 1, 0, 0, 1, true))));

        var pushedOnly = FeedMapping.Account(Json(FeedPayloadJson.Account(2002, "demo\\B", 500, "", 1000, 250, null, null, null, null, false)))!;
        Assert.AreEqual(1250m, pushedOnly.Equity);
        Assert.AreEqual(0m, pushedOnly.Margin);
        Assert.AreEqual(1250m, pushedOnly.FreeMargin);
        Assert.AreEqual("USD", pushedOnly.Currency, "an empty currency defaults like the Manager API path");
        Assert.AreEqual("", pushedOnly.Name);
        Assert.IsFalse(FeedMapping.AccountComputed(Json(FeedPayloadJson.Account(1, "g", 1, "USD", 1, 0, null, null, null, null, false))));
        Assert.IsNull(FeedMapping.Account(Json("{\"Group\":\"x\"}")));
    }

    [TestMethod]
    public void Tick_TakesBidAskAndSourceMilliseconds_DropsAnEmptyQuote()
    {
        var t = FeedMapping.Tick(Json(FeedPayloadJson.Tick("BTCUSD-", 60123.45, 60125.05, 1_789_000_000_777)))!;
        Assert.AreEqual("BTCUSD-", t.Symbol);
        Assert.AreEqual(60123.45m, t.Bid);
        Assert.AreEqual(60125.05m, t.Ask);
        Assert.AreEqual(1_789_000_000_777L, t.TimeMsc);
        Assert.IsNull(FeedMapping.Tick(Json(FeedPayloadJson.Tick("X", 0, 0, 1))));
        Assert.IsNull(FeedMapping.Tick(Json("{\"Bid\":1}")));
    }

    [TestMethod]
    public void Numbers_ToleratePayloadsThatSendStringsOrNulls()
    {
        var p = FeedMapping.Position(Json("{\"Position\":\"77\",\"Login\":null,\"VolumeExt\":\"200000000\",\"PriceOpen\":\"1.5\",\"TimeCreate\":1.0}"))!;
        Assert.AreEqual(77UL, p.PositionId);
        Assert.AreEqual(0UL, p.Login);
        Assert.AreEqual(2m, p.Volume);
        Assert.AreEqual(1.5m, p.PriceOpen);
        Assert.AreEqual(1000L, p.TimeMsc);
        Assert.AreEqual("", p.Symbol);
    }
}
