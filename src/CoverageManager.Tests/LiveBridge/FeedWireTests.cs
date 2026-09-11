using System.Text.Json;
using CoverageManager.Connector.LiveBridge;

namespace CoverageManager.Tests.LiveBridge;

/// <summary>The wire form of the contract, consumer side: every frame the bridge writes is read back, and what the consumer sends is byte-exact.</summary>
[TestClass]
public class FeedWireTests
{
    [TestMethod]
    public void Subscribe_IsTheContractsFrame_NullOnAFirstStart_NumbersWhereSeen()
    {
        var first = FeedWire.Subscribe(FeedStreams.All.ToDictionary(s => s, _ => (long?)null));
        Assert.AreEqual("{\"type\":\"subscribe\",\"streams\":[\"positions\",\"deals\",\"accounts\",\"ticks\"],\"resume\":{\"positions\":null,\"deals\":null,\"accounts\":null,\"ticks\":null}}", first);

        var later = FeedWire.Subscribe(new Dictionary<string, long?> { ["deals"] = 1789000000123, ["ticks"] = 0, ["positions"] = 7 });
        Assert.AreEqual("{\"type\":\"subscribe\",\"streams\":[\"positions\",\"deals\",\"accounts\",\"ticks\"],\"resume\":{\"positions\":7,\"deals\":1789000000123,\"accounts\":null,\"ticks\":null}}", later);
    }

    [TestMethod]
    public void Ack_CarriesTheSequencesInTheContractsOrder()
    {
        Assert.AreEqual("{\"type\":\"ack\",\"seq\":{\"positions\":1,\"deals\":2,\"ticks\":4}}",
            FeedWire.Ack(new Dictionary<string, long> { ["ticks"] = 4, ["deals"] = 2, ["positions"] = 1, ["accounts"] = 0 }));
    }

    [TestMethod]
    public void Parse_ReadsEveryFrameTheBridgeWrites()
    {
        var hello = (HelloFrame)FeedWire.Parse("{\"type\":\"hello\",\"source\":\"feed-test\",\"serverTime\":\"2026-09-10T00:26:40.123Z\",\"mode\":\"snapshot\",\"filter\":{\"streams\":[\"positions\",\"deals\",\"accounts\",\"ticks\"],\"groups\":\"everything\",\"logins\":\"everyone\",\"symbols\":\"everything\"}}");
        Assert.AreEqual(("feed-test", "snapshot", "2026-09-10T00:26:40.123Z"), (hello.Source, hello.Mode, hello.ServerTime));
        StringAssert.Contains(hello.Filter, "\"groups\":\"everything\"");

        var record = (RecordFrame)FeedWire.Parse("{\"type\":\"record\",\"source\":\"feed-test\",\"stream\":\"positions\",\"action\":\"delete\",\"seq\":1789000000200,\"at\":\"2026-09-10T00:26:40.123Z\",\"payload\":{\"Position\":1,\"ClosingDeal\":45369295}}");
        Assert.AreEqual(("feed-test", "positions", "delete", 1789000000200L), (record.Record.Source, record.Record.Stream, record.Record.Action, record.Record.Seq));
        Assert.AreEqual(45369295L, record.Record.Payload.GetProperty("ClosingDeal").GetInt64());

        var end = (EndFrame)FeedWire.Parse("{\"type\":\"snapshot_end\",\"seq\":{\"positions\":3,\"deals\":0,\"accounts\":0,\"ticks\":9}}");
        Assert.AreEqual("snapshot_end", end.Type);
        Assert.AreEqual(3, end.Seq["positions"]);
        Assert.AreEqual(9, end.Seq["ticks"]);
        Assert.AreEqual("replay_end", FeedWire.Parse("{\"type\":\"replay_end\",\"seq\":{}}").Type);

        var records = (BatchFrame)FeedWire.Parse("{\"type\":\"records\",\"stream\":\"ticks\",\"records\":[{\"source\":\"feed-test\",\"stream\":\"ticks\",\"action\":\"tick\",\"seq\":5,\"at\":\"2026-09-10T00:26:40.123Z\",\"payload\":{\"Symbol\":\"XAUUSD\",\"Bid\":4400.1,\"Ask\":4400.4,\"Last\":0,\"Volume\":0,\"TimeMsc\":1789000000001,\"Flags\":6}}]}");
        Assert.AreEqual(("records", "ticks", 1), (records.Kind, records.Stream, records.Records.Count));
        Assert.AreEqual(5L, records.Records[0].Seq);
        Assert.AreEqual("XAUUSD", records.Records[0].Payload.GetProperty("Symbol").GetString());

        var snapshot = (BatchFrame)FeedWire.Parse("{\"type\":\"snapshot\",\"stream\":\"deals\",\"records\":[]}");
        Assert.AreEqual(("snapshot", "deals", 0), (snapshot.Kind, snapshot.Stream, snapshot.Records.Count));
        var replay = (BatchFrame)FeedWire.Parse("{\"type\":\"replay\",\"stream\":\"accounts\",\"records\":[{\"action\":\"state\",\"seq\":12,\"payload\":{\"Login\":1001}}]}");
        Assert.AreEqual("accounts", replay.Records[0].Stream, "a record without its own stream takes the frame's");

        var gap = (GapFrame)FeedWire.Parse("{\"type\":\"replay_gap\",\"stream\":\"ticks\",\"reason\":\"beyond the buffer\"}");
        Assert.AreEqual(("ticks", "beyond the buffer"), (gap.Stream, gap.Reason));

        var heartbeat = (HeartbeatFrame)FeedWire.Parse("{\"type\":\"heartbeat\",\"at\":\"2026-09-10T00:26:40.123Z\",\"source\":\"disconnected\",\"seq\":{\"deals\":1}}");
        Assert.AreEqual("disconnected", heartbeat.Source);
        Assert.AreEqual(1, heartbeat.Seq["deals"]);
        Assert.IsNull(((HeartbeatFrame)FeedWire.Parse("{\"type\":\"heartbeat\",\"at\":\"x\",\"seq\":{}}")).Source);

        var bye = (ByeFrame)FeedWire.Parse("{\"type\":\"bye\",\"reason\":\"too slow: 100000 behind\"}");
        Assert.AreEqual("too slow: 100000 behind", bye.Reason);

        Assert.IsInstanceOfType(FeedWire.Parse("{\"type\":\"orders\"}"), typeof(UnknownFrame));
    }

    [TestMethod]
    public void Parse_RejectsWhatIsNotAFrame_AndDropsRecordsOfUnknownStreams()
    {
        Assert.ThrowsException<FeedProtocolException>(() => FeedWire.Parse("not json"));
        Assert.ThrowsException<FeedProtocolException>(() => FeedWire.Parse("{\"seq\":1}"));
        Assert.ThrowsException<FeedProtocolException>(() => FeedWire.Parse("[1,2]"));
        Assert.ThrowsException<FeedProtocolException>(() => FeedWire.Parse("{\"type\":\"record\",\"stream\":\"orders\",\"seq\":1,\"payload\":{}}"));
        var batch = (BatchFrame)FeedWire.Parse("{\"type\":\"records\",\"stream\":\"ticks\",\"records\":[{\"stream\":\"orders\",\"seq\":1,\"payload\":{}},{\"seq\":2,\"payload\":{}}]}");
        Assert.AreEqual(1, batch.Records.Count);
        Assert.AreEqual(JsonValueKind.Object, batch.Records[0].Payload.ValueKind);
    }
}
