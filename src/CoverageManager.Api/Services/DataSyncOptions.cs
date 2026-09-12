namespace CoverageManager.Api.Services;

/// <summary>
/// Settings for the incremental deal persistence (<see cref="DataSyncService"/>), bound from the <c>DataSync</c>
/// section. The defaults sustain the Live Bridge feed's rate (about 20,000 deals an hour) with a handful of writes
/// per tick, and drain an outage backlog at up to <c>ChunkSize * MaxChunksPerTick</c> deals per tick.
/// </summary>
public sealed class DataSyncOptions
{
    public const string SectionName = "DataSync";

    /// <summary>How often pending deals are written.</summary>
    public int IntervalMs { get; set; } = 30_000;

    /// <summary>Rows per upsert request (PostgREST batch).</summary>
    public int ChunkSize { get; set; } = 500;

    /// <summary>Most requests per tick; the rest of the backlog waits for the next tick.</summary>
    public int MaxChunksPerTick { get; set; } = 20;

    /// <summary>Pause between two requests of the same tick.</summary>
    public int ChunkPauseMs { get; set; } = 250;

    /// <summary>Per-request ceiling; the shared HttpClient's own timeout is far longer.</summary>
    public int RequestTimeoutMs { get; set; } = 30_000;

    /// <summary>Persisted deals older than this are forgotten by the in-memory store (the durable store keeps them).</summary>
    public int RetentionHours { get; set; } = 48;

    /// <summary>Above this many deals in memory the oldest are dropped even if not yet persisted (a long outage).</summary>
    public int HardCap { get; set; } = 2_000_000;
}

/// <summary>A snapshot of the deal persistence for <c>/api/exposure/diagnostics.dealSync</c>.</summary>
public sealed record DealSyncStatus
{
    public int Pending { get; init; }
    public int PendingModifications { get; init; }
    public int StoreCount { get; init; }
    public DateTime? StoreEarliestUtc { get; init; }
    public DateTime? LastSuccessUtc { get; init; }
    public DateTime? LastFailureUtc { get; init; }
    public string? LastError { get; init; }
    public int ConsecutiveFailures { get; init; }
    public long SyncedTotal { get; init; }
    public long RequestsTotal { get; init; }
    public int LastTickRows { get; init; }
    public int LastTickChunks { get; init; }
    public long EvictedTotal { get; init; }
    public long EvictedUnsyncedTotal { get; init; }
    public long AuditEntriesTotal { get; init; }
}
