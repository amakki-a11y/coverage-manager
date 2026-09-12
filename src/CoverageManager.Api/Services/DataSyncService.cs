using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CoverageManager.Core.Engines;
using CoverageManager.Core.Models;

namespace CoverageManager.Api.Services;

/// <summary>
/// Persists deals to Supabase incrementally. Every <see cref="DataSyncOptions.IntervalMs"/> it takes the deals the
/// <see cref="DealStore"/> queued as added or changed since their last successful write, sends them in paced chunks,
/// marks them synced, and forgets persisted deals older than the retention window. Dealer edits are audited from the
/// change the store recorded, without reading Supabase back. Sustained cost at the Live Bridge feed's rate (about
/// 20,000 deals an hour): a few requests a minute. An outage backlog drains at ChunkSize x MaxChunksPerTick deals
/// per tick; while Supabase refuses, one probe request per tick.
///
/// <para>Until 2026-09-11 this service re-upserted the whole in-memory store every 30 s and read every one of those
/// deals back for change detection: at feed scale that was tens of thousands of requests an hour, and it took
/// Supabase down. Counters: <c>/api/exposure/diagnostics.dealSync</c>.</para>
/// </summary>
public sealed class DataSyncService : BackgroundService
{
    private readonly IDataStore _supabase;
    private readonly DealStore _dealStore;
    private readonly PositionManager _positionManager;
    private readonly ILogger<DataSyncService> _logger;
    private readonly DataSyncOptions _options;
    private readonly object _statusGate = new();
    private DealSyncStatus _status = new();
    private int _consecutiveFailures;

    public DataSyncService(
        IDataStore supabase,
        DealStore dealStore,
        PositionManager positionManager,
        ILogger<DataSyncService> logger,
        DataSyncOptions? options = null)
    {
        _supabase = supabase;
        _dealStore = dealStore;
        _positionManager = positionManager;
        _logger = logger;
        _options = options ?? new DataSyncOptions();
    }

    /// <summary>Live counters for <c>/api/exposure/diagnostics.dealSync</c>.</summary>
    public DealSyncStatus Status
    {
        get
        {
            DealSyncStatus s;
            lock (_statusGate) s = _status;
            return s with
            {
                Pending = _dealStore.PendingCount,
                PendingModifications = _dealStore.PendingModificationCount,
                StoreCount = _dealStore.DealCount,
                StoreEarliestUtc = _dealStore.EarliestDealTime,
            };
        }
    }

    /// <summary>
    /// Resolve an MT5 raw symbol to its canonical name via symbol_mappings, with
    /// a deterministic fallback so queries still group correctly if the mapping
    /// is missing: strip trailing <c>-</c> / <c>.c</c> / <c>.m</c> and uppercase.
    /// Matches what the settled-P&amp;L RPC does server-side so client- and
    /// server-computed canonical keys stay in lockstep.
    /// </summary>
    private string ResolveCanonical(string rawSymbol)
    {
        var m = _positionManager.FindMapping(rawSymbol, "bbook");
        if (m != null) return m.CanonicalName.ToUpperInvariant();

        var s = (rawSymbol ?? string.Empty).Trim();
        var dot = s.LastIndexOf('.');
        if (dot >= 0 && s.Length - dot <= 3) s = s.Substring(0, dot);
        while (s.EndsWith("-")) s = s[..^1];
        return s.ToUpperInvariant();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(5000, stoppingToken); // Let other services start first
        _logger.LogInformation(
            "DataSyncService started: incremental deal sync every {Interval}s, up to {Chunks} x {Rows} rows per tick, {Retention} h retention, hard cap {Cap}",
            _options.IntervalMs / 1000, _options.MaxChunksPerTick, _options.ChunkSize, _options.RetentionHours, _options.HardCap);

        await LoadDealsFromSupabaseAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DataSyncService error during sync cycle");
            }

            try { await Task.Delay(_options.IntervalMs, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("DataSyncService stopped ({Pending} deals pending)", _dealStore.PendingCount);
    }

    /// <summary>
    /// On startup, load today's deals from Supabase into the in-memory DealStore so the aggregations survive a
    /// restart. They are held, not queued: they came from the store.
    /// </summary>
    private async Task LoadDealsFromSupabaseAsync()
    {
        try
        {
            var todayStart = DateTime.UtcNow.Date;
            var todayEnd = todayStart.AddDays(1);

            var lastDealTime = await _supabase.GetLastDealTimeAsync("bbook");
            var loadFrom = todayStart;
            if (lastDealTime.HasValue && lastDealTime.Value < todayStart)
            {
                loadFrom = lastDealTime.Value.Date;
                _logger.LogInformation("Detected deals before today (last: {LastDeal}), loading from {From}", lastDealTime.Value, loadFrom);
            }

            var deals = await _supabase.GetDealsAsync("bbook", loadFrom, todayEnd);
            if (deals.Count == 0)
            {
                _logger.LogInformation("No deals found in Supabase for today");
                return;
            }

            var added = _dealStore.Load(deals.Select(d => new ClosedDeal
            {
                DealId = (ulong)d.DealId,
                Login = (ulong)d.Login,
                Symbol = d.Symbol,
                Direction = d.Direction,
                VolumeLots = d.Volume,
                Price = d.Price,
                Profit = d.Profit,
                Commission = d.Commission,
                Swap = d.Swap,
                Fee = d.Fee,
                Entry = (uint)d.Entry,
                Action = (uint)d.Action,
                OrderId = d.OrderId.HasValue ? (ulong)d.OrderId.Value : 0UL,
                PositionId = d.PositionId.HasValue ? (ulong)d.PositionId.Value : 0UL,
                // Supabase timestamps deserialize with Kind=Local on a non-UTC box; the store compares instants.
                Time = d.DealTime.Kind == DateTimeKind.Local ? d.DealTime.ToUniversalTime() : DateTime.SpecifyKind(d.DealTime, DateTimeKind.Utc)
            }));
            _logger.LogInformation("Loaded {Count} deals from Supabase into DealStore on startup (from {From}; {New} not held yet)", deals.Count, loadFrom, added);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load deals from Supabase on startup");
        }
    }

    /// <summary>One tick: audit recorded modifications, write the pending deals in paced chunks, prune the store.</summary>
    private async Task SyncTickAsync(CancellationToken ct)
    {
        var chunkSize = Math.Max(1, _options.ChunkSize);
        var maxChunks = _consecutiveFailures > 0 ? 1 : Math.Max(1, _options.MaxChunksPerTick);   // probe gently while Supabase is unwell
        var audited = await AuditModificationsAsync(ct);

        var batch = _dealStore.TakePending(chunkSize * maxChunks);
        if (batch.Count > 0)
        {
            var moved = await _supabase.GetMovedLoginsAsync();
            if (moved.Count > 0)
            {
                var skipped = batch.Where(p => moved.Contains((long)p.Deal.Login)).ToList();
                if (skipped.Count > 0)
                {
                    _dealStore.MarkSynced(skipped);   // moved accounts are never written
                    batch = batch.Where(p => !moved.Contains((long)p.Deal.Login)).ToList();
                }
            }
        }

        var rows = 0;
        var chunks = 0;
        string? error = null;
        var timeout = TimeSpan.FromMilliseconds(Math.Max(1000, _options.RequestTimeoutMs));
        foreach (var chunk in batch.Chunk(chunkSize))
        {
            if (chunks > 0 && _options.ChunkPauseMs > 0) await Task.Delay(_options.ChunkPauseMs, ct);
            chunks++;
            var records = chunk.Select(p => ToRecord(p.Deal)).ToList();
            var (ok, err) = await _supabase.UpsertDealChunkAsync(records, timeout, ct);
            if (!ok) { error = err; break; }
            _dealStore.MarkSynced(chunk);
            rows += chunk.Length;
        }

        var prune = _dealStore.Prune(DateTime.UtcNow.AddHours(-Math.Max(1, _options.RetentionHours)), _options.HardCap);

        _consecutiveFailures = error is null ? 0 : _consecutiveFailures + 1;
        var now = DateTime.UtcNow;
        lock (_statusGate)
        {
            _status = _status with
            {
                LastTickRows = rows,
                LastTickChunks = chunks,
                SyncedTotal = _status.SyncedTotal + rows,
                RequestsTotal = _status.RequestsTotal + chunks,
                EvictedTotal = _status.EvictedTotal + prune.EvictedSynced + prune.EvictedUnsynced,
                EvictedUnsyncedTotal = _status.EvictedUnsyncedTotal + prune.EvictedUnsynced,
                AuditEntriesTotal = _status.AuditEntriesTotal + audited,
                LastSuccessUtc = rows > 0 ? now : _status.LastSuccessUtc,
                LastFailureUtc = error is null ? _status.LastFailureUtc : now,
                LastError = error ?? (rows > 0 ? null : _status.LastError),
                ConsecutiveFailures = _consecutiveFailures,
            };
        }

        if (error is not null)
            _logger.LogWarning("Deal sync: request failed ({Error}); {Rows} rows written this tick, {Pending} deals pending, failure #{Failures}; next tick sends one probe request",
                error, rows, _dealStore.PendingCount, _consecutiveFailures);
        else if (rows > 0)
            _logger.LogInformation("Deal sync: {Rows} deals in {Chunks} request(s); {Pending} pending", rows, chunks, _dealStore.PendingCount);
        if (prune.EvictedUnsynced > 0)
            _logger.LogWarning("Deal store over its hard cap of {Cap}: {Count} deals dropped from memory before they reached Supabase", _options.HardCap, prune.EvictedUnsynced);
        else if (prune.EvictedSynced > 0)
            _logger.LogDebug("Deal store: forgot {Count} persisted deals older than {Hours} h ({Remaining} held)", prune.EvictedSynced, _options.RetentionHours, prune.Remaining);
    }

    /// <summary>Audit rows for the deals that arrived with changed values, from the copies the store held; no Supabase read.</summary>
    private async Task<int> AuditModificationsAsync(CancellationToken ct)
    {
        var mods = _dealStore.TakeModifications();
        if (mods.Count == 0) return 0;
        var entries = mods.SelectMany(m => DealChangeAudit.Entries(m, "bbook")).ToList();
        if (entries.Count == 0) return 0;
        var ok = await _supabase.TryInsertAuditEntriesAsync(entries, ct);
        if (!ok)
        {
            _dealStore.RequeueModifications(mods);
            return 0;
        }
        _logger.LogWarning("Detected {Count} deal modifications by dealer/admin ({Entries} audit entries)", mods.Count, entries.Count);
        return entries.Count;
    }

    private DealRecord ToRecord(ClosedDeal d) => new()
    {
        DealId = (long)d.DealId,
        Source = "bbook",
        Login = (long)d.Login,
        Symbol = d.Symbol,
        CanonicalSymbol = ResolveCanonical(d.Symbol),
        Direction = d.Direction,
        // Preserve the real MT5 DealAction code (0=BUY, 1=SELL, 2=BALANCE, 3=CREDIT, 4=CHARGE, 5=CORRECTION, ...):
        // balance/credit deals feed the Equity P&L tab and must not be re-classified as trades.
        Action = (int)d.Action,
        Entry = (int)d.Entry,
        Volume = d.VolumeLots,
        Price = d.Price,
        Profit = d.Profit,
        Commission = d.Commission,
        Swap = d.Swap,
        Fee = d.Fee,
        OrderId = d.OrderId == 0 ? null : (long?)d.OrderId,
        PositionId = d.PositionId == 0 ? null : (long?)d.PositionId,
        DealTime = d.Time
    };
}
