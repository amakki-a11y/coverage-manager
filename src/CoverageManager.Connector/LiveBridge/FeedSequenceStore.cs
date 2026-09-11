using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoverageManager.Connector.LiveBridge;

/// <summary>
/// The last applied sequence per stream, kept durably in a small JSON file so a restart resumes where it left off
/// (contract: "keep the last sequence per stream durably"). Writes are debounced (<see cref="LiveBridgeOptions.SequenceFlushMs"/>)
/// and atomic (temp file + move); <see cref="Flush"/> is called at every handshake end, on every drop and on dispose.
/// A crash between two flushes costs at most the debounce window: the bridge replays those records, and applying them
/// again is idempotent. The file names the feed URL it belongs to; a file for another feed is ignored.
/// </summary>
public sealed class FeedSequenceStore : IDisposable
{
    private readonly string _url;
    private readonly TimeSpan _flushDelay;
    private readonly ILogger _logger;
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _seq = new(StringComparer.Ordinal);
    private Timer? _timer;
    private bool _dirty;
    private bool _disposed;

    public FeedSequenceStore(string path, string url, TimeSpan flushDelay, ILogger? logger = null)
    {
        Path = string.IsNullOrWhiteSpace(path) ? throw new ArgumentException("a state path is required", nameof(path)) : path;
        _url = url ?? "";
        _flushDelay = flushDelay < TimeSpan.Zero ? TimeSpan.Zero : flushDelay;
        _logger = logger ?? NullLogger.Instance;
        foreach (var s in FeedStreams.All) _seq[s] = 0;
    }

    public string Path { get; }
    public DateTime? LastSavedUtc { get; private set; }

    /// <summary>What <see cref="Load"/> found, for the startup log.</summary>
    public string LoadNote { get; private set; } = "not loaded";

    /// <summary>Reads the file; missing, unreadable or for another feed = start from nothing (a snapshot).</summary>
    public void Load()
    {
        lock (_gate)
        {
            if (!File.Exists(Path))
            {
                LoadNote = "no state file yet (first start): the feed will send a snapshot";
                return;
            }
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(Path, Encoding.UTF8));
                var root = doc.RootElement;
                var url = root.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
                if (!string.Equals(url, _url, StringComparison.Ordinal))
                {
                    LoadNote = $"state file belongs to another feed ({url}): starting with a snapshot";
                    return;
                }
                if (root.TryGetProperty("seq", out var seq) && seq.ValueKind == JsonValueKind.Object)
                    foreach (var p in seq.EnumerateObject())
                        if (FeedStreams.IsKnown(p.Name) && p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt64(out var v) && v > 0)
                            _seq[p.Name] = v;
                var savedAt = root.TryGetProperty("savedAt", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : "?";
                LoadNote = $"resuming from the sequences saved {savedAt}: " + Describe();
            }
            catch (Exception ex)
            {
                foreach (var s in FeedStreams.All) _seq[s] = 0;
                LoadNote = $"state file unreadable ({ex.GetType().Name}: {ex.Message}): starting with a snapshot";
            }
        }
    }

    public long Get(string stream)
    {
        lock (_gate) return _seq.TryGetValue(stream, out var v) ? v : 0;
    }

    public IReadOnlyDictionary<string, long> Snapshot()
    {
        lock (_gate) return new Dictionary<string, long>(_seq, StringComparer.Ordinal);
    }

    /// <summary>The resume map for a subscribe: the sequence per stream, null where nothing was seen yet.</summary>
    public IReadOnlyDictionary<string, long?> Resume()
    {
        lock (_gate)
            return FeedStreams.All.ToDictionary(s => s, s => _seq[s] > 0 ? _seq[s] : (long?)null, StringComparer.Ordinal);
    }

    /// <summary>Moves a stream's sequence forward; a value at or below the current one is ignored.</summary>
    public bool Advance(string stream, long seq)
    {
        lock (_gate)
        {
            if (_disposed || !FeedStreams.IsKnown(stream) || seq <= _seq[stream]) return false;
            _seq[stream] = seq;
            _dirty = true;
            if (_flushDelay == TimeSpan.Zero)
            {
                FlushLocked();
                return true;
            }
            _timer ??= new Timer(_ => Flush(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _timer.Change(_flushDelay, Timeout.InfiniteTimeSpan);
            return true;
        }
    }

    /// <summary>Writes the file now if anything changed since the last write.</summary>
    public void Flush()
    {
        lock (_gate)
        {
            if (_dirty) FlushLocked();
        }
    }

    private void FlushLocked()
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var temp = Path + ".tmp";
            File.WriteAllText(temp, Serialize(), new UTF8Encoding(false));
            File.Move(temp, Path, overwrite: true);
            _dirty = false;
            LastSavedUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Live Bridge: could not save the sequence file {Path}", Path);
        }
    }

    private string Serialize()
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteNumber("version", 1);
            w.WriteString("url", _url);
            w.WritePropertyName("seq");
            w.WriteStartObject();
            foreach (var s in FeedStreams.All) w.WriteNumber(s, _seq[s]);
            w.WriteEndObject();
            w.WriteString("savedAt", DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private string Describe() => string.Join(", ", FeedStreams.All.Select(s => $"{s}={_seq[s]}"));

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
            if (_dirty) FlushLocked();
        }
    }
}
