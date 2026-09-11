using System.Text;
using System.Text.Json;

namespace CoverageManager.Connector.LiveBridge;

/// <summary>The four streams of the Live Bridge consumer feed, in the contract's order.</summary>
public static class FeedStreams
{
    public const string Positions = "positions";
    public const string Deals = "deals";
    public const string Accounts = "accounts";
    public const string Ticks = "ticks";

    public static readonly IReadOnlyList<string> All = new[] { Positions, Deals, Accounts, Ticks };

    public static bool IsKnown(string? stream) => stream is not null && All.Contains(stream, StringComparer.Ordinal);

    /// <summary>0..3 in the contract's order, -1 for anything else.</summary>
    public static int IndexOf(string? stream) => stream switch
    {
        Positions => 0,
        Deals => 1,
        Accounts => 2,
        Ticks => 3,
        _ => -1,
    };
}

/// <summary>Record actions: positions add / update / delete; a deal is an add; an account frame is its state; a tick is a tick.</summary>
public static class FeedActions
{
    public const string Add = "add";
    public const string Update = "update";
    public const string Delete = "delete";
    public const string State = "state";
    public const string Tick = "tick";
}

/// <summary>One record as the feed sends it: the envelope, and the payload with MT5's names in PascalCase.</summary>
public sealed record FeedRecord(string Source, string Stream, string Action, long Seq, string At, JsonElement Payload);

/// <summary>A frame from the bridge. <see cref="Type"/> is the wire type.</summary>
public abstract record FeedFrame(string Type);

/// <summary>The first frame after a subscribe: the source, the server's time, the mode (snapshot or resume) and the filter in force.</summary>
public sealed record HelloFrame(string Source, string ServerTime, string Mode, string Filter) : FeedFrame("hello");

/// <summary>A batch of one stream's records: <c>snapshot</c>, <c>replay</c>, or live <c>records</c> (ticks bundled).</summary>
public sealed record BatchFrame(string Kind, string Stream, IReadOnlyList<FeedRecord> Records) : FeedFrame(Kind);

/// <summary><c>snapshot_end</c> or <c>replay_end</c>: the handshake is complete; the sequences to resume from.</summary>
public sealed record EndFrame(string Kind, IReadOnlyDictionary<string, long> Seq) : FeedFrame(Kind);

/// <summary>A stream could not be replayed exactly (ticks beyond the ring, deals older than the store keeps).</summary>
public sealed record GapFrame(string Stream, string Reason) : FeedFrame("replay_gap");

/// <summary>One live record.</summary>
public sealed record RecordFrame(FeedRecord Record) : FeedFrame("record");

/// <summary>Nothing else was sent for a while: the bridge is alive, these are its sequences, and whether the MT5 source is connected.</summary>
public sealed record HeartbeatFrame(string At, IReadOnlyDictionary<string, long> Seq, string? Source) : FeedFrame("heartbeat");

/// <summary>The bridge closes the connection for this reason.</summary>
public sealed record ByeFrame(string Reason) : FeedFrame("bye");

/// <summary>A type this consumer does not know; ignored and counted.</summary>
public sealed record UnknownFrame(string Kind) : FeedFrame(Kind);

/// <summary>The bridge sent something that is not the contract.</summary>
public sealed class FeedProtocolException : Exception
{
    public FeedProtocolException(string message) : base(message) { }
}

/// <summary>The feed's wire form, consumer side: reads the bridge's JSON text frames, writes subscribe and ack.</summary>
public static class FeedWire
{
    /// <summary>
    /// <c>{"type":"subscribe","streams":[...],"resume":{"positions":N|null,...}}</c> - every stream, in the contract's
    /// order; null asks for a snapshot of that stream, a number for what was missed since it.
    /// </summary>
    public static string Subscribe(IReadOnlyDictionary<string, long?> resume)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("type", "subscribe");
            w.WritePropertyName("streams");
            w.WriteStartArray();
            foreach (var s in FeedStreams.All) w.WriteStringValue(s);
            w.WriteEndArray();
            w.WritePropertyName("resume");
            w.WriteStartObject();
            foreach (var s in FeedStreams.All)
            {
                if (resume.TryGetValue(s, out var seq) && seq is > 0) w.WriteNumber(s, seq.Value);
                else w.WriteNull(s);
            }
            w.WriteEndObject();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary><c>{"type":"ack","seq":{...}}</c> - the last applied sequence per stream (streams at 0 omitted).</summary>
    public static string Ack(IReadOnlyDictionary<string, long> seq)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("type", "ack");
            w.WritePropertyName("seq");
            w.WriteStartObject();
            foreach (var s in FeedStreams.All)
                if (seq.TryGetValue(s, out var v) && v > 0) w.WriteNumber(s, v);
            w.WriteEndObject();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Reads one text frame. Throws <see cref="FeedProtocolException"/> when it is not JSON or has no type.</summary>
    public static FeedFrame Parse(string json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new FeedProtocolException("frame is not JSON: " + ex.Message); }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("type", out var typeElement)
                || typeElement.ValueKind != JsonValueKind.String)
                throw new FeedProtocolException("a frame is an object with a type");

            var type = typeElement.GetString()!;
            switch (type)
            {
                case "hello":
                    return new HelloFrame(
                        Str(root, "source"), Str(root, "serverTime"), Str(root, "mode"),
                        root.TryGetProperty("filter", out var filter) ? filter.GetRawText() : "");

                case "snapshot":
                case "replay":
                case "records":
                {
                    var stream = Str(root, "stream");
                    return new BatchFrame(type, stream, ReadRecords(root, stream));
                }

                case "snapshot_end":
                case "replay_end":
                    return new EndFrame(type, ReadSeq(root));

                case "replay_gap":
                    return new GapFrame(Str(root, "stream"), Str(root, "reason"));

                case "record":
                    return new RecordFrame(ReadRecord(root, null) ?? throw new FeedProtocolException("record frame without a known stream"));

                case "heartbeat":
                    return new HeartbeatFrame(
                        Str(root, "at"), ReadSeq(root),
                        root.TryGetProperty("source", out var src) && src.ValueKind == JsonValueKind.String ? src.GetString() : null);

                case "bye":
                    return new ByeFrame(Str(root, "reason"));

                default:
                    return new UnknownFrame(type);
            }
        }
    }

    private static IReadOnlyList<FeedRecord> ReadRecords(JsonElement root, string frameStream)
    {
        var list = new List<FeedRecord>();
        if (!root.TryGetProperty("records", out var records) || records.ValueKind != JsonValueKind.Array) return list;
        foreach (var element in records.EnumerateArray())
        {
            var record = ReadRecord(element, frameStream);
            if (record is not null) list.Add(record);
        }
        return list;
    }

    private static FeedRecord? ReadRecord(JsonElement element, string? fallbackStream)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        var stream = element.TryGetProperty("stream", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : fallbackStream;
        if (!FeedStreams.IsKnown(stream)) return null;
        var seq = element.TryGetProperty("seq", out var q) && q.ValueKind == JsonValueKind.Number && q.TryGetInt64(out var v) ? v : 0;
        var payload = element.TryGetProperty("payload", out var p) ? p.Clone() : default;
        return new FeedRecord(Str(element, "source"), stream!, Str(element, "action"), seq, Str(element, "at"), payload);
    }

    private static IReadOnlyDictionary<string, long> ReadSeq(JsonElement root)
    {
        var seq = new Dictionary<string, long>(StringComparer.Ordinal);
        if (root.TryGetProperty("seq", out var element) && element.ValueKind == JsonValueKind.Object)
            foreach (var p in element.EnumerateObject())
                if (FeedStreams.IsKnown(p.Name) && p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt64(out var v))
                    seq[p.Name] = v;
        return seq;
    }

    private static string Str(JsonElement element, string name)
        => element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
