using CoverageManager.Core.Models;
using CoverageManager.Core.Models.Bridge;
using CoverageManager.Core.Models.EquityPnL;

namespace CoverageManager.Api.Services;

/// <summary>
/// The backend's data-store contract — every domain read/write the app needs.
/// Implemented by <see cref="SupabaseService"/> (v1, PostgREST over HTTP) and by
/// <see cref="PostgresService"/> (v2, direct Npgsql to the local coverage_v2
/// database). Controllers, services and engine writers depend on this interface,
/// so the store is swapped by DI registration only — call sites are unchanged.
///
/// Conventions preserved across both implementations:
///   * async, self-logging, return an "empty" result on failure (empty list,
///     null, false / 0) rather than throwing.
///   * upserts are idempotent on the natural conflict key.
/// </summary>
public interface IDataStore
{
    // ── Symbol mappings ──
    Task<List<SymbolMapping>> GetMappingsAsync();
    Task<SymbolMapping?> UpsertMappingAsync(SymbolMapping mapping);
    Task<bool> DeleteMappingAsync(Guid id);

    // ── Account settings ──
    Task<List<AccountSettings>> GetAccountSettingsAsync();
    Task<AccountSettings?> CreateAccountSettingsAsync(AccountSettings settings);
    Task<AccountSettings?> UpdateAccountSettingsAsync(Guid id, AccountSettings settings);
    Task<bool> DeleteAccountSettingsAsync(Guid id);

    // ── Bridge settings (Centroid) ──
    Task<BridgeSettings?> GetBridgeSettingsAsync();
    Task<BridgeSettings?> UpsertBridgeSettingsAsync(BridgeSettings settings);

    // ── Bridge executions (paired CLIENT <-> COV_OUT dropcopy fills) ──
    // Unique, not reconstructible from the feed, so it lives in the same store as
    // everything else in v2 rather than in Supabase behind its own HTTP client.
    Task<int> UpsertBridgeExecutionsAsync(IReadOnlyCollection<ExecutionPair> pairs, CancellationToken ct = default);
    Task<IReadOnlyList<ExecutionPair>> QueryBridgeExecutionsAsync(
        DateTime fromUtc, DateTime toUtc, string? canonicalSymbol, int limit, CancellationToken ct = default);

    // ── Trading accounts ──
    Task<List<TradingAccount>> GetTradingAccountsAsync(string? source = null);
    Task<int> UpsertTradingAccountsAsync(IEnumerable<TradingAccount> accounts);

    // ── Moved accounts ──
    Task<HashSet<long>> GetMovedLoginsAsync(bool forceRefresh = false);
    Task<List<Dictionary<string, object>>> GetMovedAccountsAsync();

    // ── Deals ──
    Task<List<DealRecord>> GetNonTradeDealsAsync(string source, DateTime from, DateTime to);
    Task<List<DealRecord>> GetTradeDealsForLoginsAsync(string source, IEnumerable<long> logins, DateTime fromUtc, DateTime toUtc);
    Task<Dictionary<long, decimal>> SumTradeBalanceFlowPerLoginAsync(string source, DateTime fromUtc, DateTime toUtc);
    Task<List<DealRecord>> GetDealsAsync(string source, DateTime from, DateTime to);
    Task<DateTime?> GetLastDealTimeAsync(string source);
    Task<int> DeleteDealsAsync(string source, IEnumerable<long> dealIds);
    Task<int> UpsertDealsAsync(IEnumerable<DealRecord> deals);
    Task<int> DetectAndLogDealChangesAsync(IEnumerable<DealRecord> incomingDeals, string source);

    /// <summary>
    /// One chunk of deals, upserted on (source, deal_id). True when accepted; otherwise the
    /// reason, short. The caller owns pacing/retries and bounds each write with
    /// <paramref name="timeout"/> so a stalled store cannot hold a sync tick.
    /// </summary>
    Task<(bool Ok, string? Error)> UpsertDealChunkAsync(IReadOnlyList<DealRecord> chunk, TimeSpan timeout, CancellationToken ct = default);

    /// <summary>Audit rows in one go; false when the store did not accept them (caller re-queues).</summary>
    Task<bool> TryInsertAuditEntriesAsync(IEnumerable<TradeAuditEntry> entries, CancellationToken ct = default);

    /// <summary>
    /// Compare incoming deals with the stored copies already in hand (no read), detect changes,
    /// and log audit entries. Used by the reconciliation sweep, which already fetched the window.
    /// </summary>
    Task<int> DetectAndLogDealChangesAsync(IEnumerable<DealRecord> incomingDeals, IReadOnlyDictionary<long, DealRecord> existing, string source);

    // ── Deal retention (12-month rolling window, V2_PLAN §5.5) ──
    /// <summary>
    /// Deletes deals with <c>deal_time &lt; cutoffUtc</c> in batches, and nothing else. Returns rows deleted
    /// and batches used. Unlike the rest of this surface it THROWS on failure: for a destructive
    /// operation a swallowed error would be recorded as a successful "deleted 0".
    /// </summary>
    Task<(long Deleted, int Batches)> DeleteDealsOlderThanAsync(DateTime cutoffUtc, int batchSize, CancellationToken ct = default);
    Task<RetentionPruneRun?> InsertRetentionPruneRunAsync(RetentionPruneRun run);
    Task<List<RetentionPruneRun>> ListRetentionPruneRunsAsync(int limit = 30);

    // ── Audit log ──
    Task InsertAuditEntriesAsync(IEnumerable<TradeAuditEntry> entries);
    Task<List<TradeAuditEntry>> GetAuditLogAsync(DateTime? from = null, string? symbol = null, long? login = null);

    // ── Alert rules ──
    Task<List<RiskThreshold>> GetAlertRulesAsync();
    Task<RiskThreshold?> UpsertAlertRuleAsync(RiskThreshold rule);
    Task<bool> DeleteAlertRuleAsync(Guid id);

    // ── Alert events ──
    Task<int> InsertAlertEventsAsync(IEnumerable<AlertEvent> events);
    Task<List<AlertEvent>> GetAlertEventsAsync(bool unacknowledgedOnly = false, int limit = 100);
    Task<bool> AcknowledgeAlertEventAsync(Guid id);

    // ── Exposure snapshots ──
    Task<int> UpsertExposureSnapshotsAsync(IEnumerable<ExposureSnapshot> snapshots);
    Task<Dictionary<string, ExposureSnapshot>> GetNearestSnapshotsBeforeAsync(DateTime anchorUtc);
    Task<Dictionary<string, ExposureSnapshot>> GetSnapshotsAtAsync(DateTime exactSnapshotTimeUtc);
    Task<List<ExposureSnapshot>> ListExposureSnapshotsAsync(DateTime fromUtc, DateTime toUtc, string? canonicalSymbol = null);

    // ── Aggregation RPCs (local SQL functions in migration 0090) ──
    Task<Dictionary<string, decimal>> AggregateBBookSettledPnlAsync(DateTime fromUtc, DateTime toUtc, IEnumerable<long> excludedLogins);
    Task<List<SymbolPnL>> AggregateBBookPnLFullAsync(DateTime fromUtc, DateTime toUtc, IEnumerable<long> excludedLogins);

    // ── Reconciliation runs ──
    Task<ReconciliationRun?> InsertReconciliationRunAsync(ReconciliationRun run);
    Task<List<ReconciliationRun>> ListReconciliationRunsAsync(int limit = 50);

    // ── Snapshot schedules ──
    Task<List<SnapshotSchedule>> GetSnapshotSchedulesAsync();
    Task<SnapshotSchedule?> UpsertSnapshotScheduleAsync(SnapshotSchedule schedule);
    Task<bool> DeleteSnapshotScheduleAsync(Guid id);

    // ── Equity P&L: account equity snapshots ──
    Task<int> UpsertAccountEquitySnapshotsAsync(IEnumerable<AccountEquitySnapshot> rows);
    Task<Dictionary<string, AccountEquitySnapshot>> GetAccountEquitySnapshotsBeforeAsync(DateTime anchorUtc);
    Task<List<AccountEquitySnapshot>> GetAccountEquitySnapshotsInRangeAsync(long login, string source, DateTime fromUtc, DateTime toUtc);

    // ── Equity P&L: per-login config + spread rebates ──
    Task<List<EquityPnLClientConfig>> GetEquityPnLClientConfigsAsync(string? source = null);
    Task<bool> UpsertEquityPnLClientConfigAsync(EquityPnLClientConfig cfg);
    Task<int> UpsertEquityPnLClientConfigsAsync(IEnumerable<EquityPnLClientConfig> cfgs);
    Task<List<SpreadRebateRate>> GetSpreadRebateRatesAsync(long? login = null);
    Task<int> UpsertSpreadRebateRatesAsync(IEnumerable<SpreadRebateRate> rates);
    Task<bool> DeleteSpreadRebateRateAsync(long login, string source, string canonicalSymbol);

    // ── Equity P&L Phase 2: login groups ──
    Task<List<LoginGroup>> GetLoginGroupsAsync();
    Task<LoginGroup?> UpsertLoginGroupAsync(LoginGroup g);
    Task<bool> DeleteLoginGroupAsync(Guid id);
    Task<List<LoginGroupMember>> GetLoginGroupMembersAsync(Guid? groupId = null);
    Task<bool> AddLoginGroupMemberAsync(LoginGroupMember m);
    Task<bool> RemoveLoginGroupMemberAsync(Guid groupId, long login, string source);
    Task<List<EquityPnLGroupConfig>> GetGroupConfigsAsync();
    Task<bool> UpsertGroupConfigAsync(EquityPnLGroupConfig cfg);
    Task<List<GroupSpreadRebateRate>> GetGroupSpreadRebateRatesAsync(Guid? groupId = null);
    Task<bool> UpsertGroupSpreadRebateRatesAsync(IEnumerable<GroupSpreadRebateRate> rates);
    Task<bool> DeleteGroupSpreadRebateRateAsync(Guid groupId, string canonicalSymbol);
}
