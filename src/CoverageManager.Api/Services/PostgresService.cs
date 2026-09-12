using System.Data;
using CoverageManager.Core.Models;
using CoverageManager.Core.Models.Bridge;
using CoverageManager.Core.Models.EquityPnL;
using Dapper;
using Npgsql;

namespace CoverageManager.Api.Services;

/// <summary>
/// v2 data store: direct Npgsql access to the local <c>coverage_v2</c> PostgreSQL
/// database. Drop-in replacement for <see cref="SupabaseService"/> — implements the
/// same <see cref="IDataStore"/> surface with identical semantics (same filters,
/// ordering, conflict keys, and "return empty on failure" error handling), so call
/// sites are unchanged and only the DI registration differs.
///
/// <para>Design notes:
///   * Reads use Dapper with <c>MatchNamesWithUnderscores</c> so snake_case columns
///     map onto the PascalCase DTO properties (bbook_symbol -&gt; BBookSymbol, ...).
///   * The four v1 Supabase RPCs are the local SQL functions from migration 0090
///     (<c>aggregate_bbook_settled_pnl</c>, <c>aggregate_bbook_pnl_full</c>,
///     <c>latest_snapshots_before</c>) — called directly here.
///   * Every DateTime bound to a timestamptz is normalized to UTC (<see cref="U"/>)
///     so Npgsql never rejects an Unspecified/Local Kind; reads come back as UTC.
///   * The two <c>date</c> columns (ps_contract_start, ps_last_processed_month) are
///     bound as 'yyyy-MM-dd' strings and cast <c>::date</c> so no timezone shift
///     can occur.
/// </para>
/// </summary>
public class PostgresService : IDataStore
{
    private readonly string _cs;
    private readonly ILogger<PostgresService> _logger;

    static PostgresService()
    {
        // snake_case columns -> PascalCase properties for Dapper materialization.
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public PostgresService(IConfiguration config, ILogger<PostgresService> logger)
    {
        _logger = logger;
        _cs = BuildConnectionString(config);
    }

    private static string BuildConnectionString(IConfiguration config)
    {
        var explicitCs = config["Postgres:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(explicitCs)) return explicitCs;

        var b = new NpgsqlConnectionStringBuilder
        {
            Host = config["Postgres:Host"] ?? "127.0.0.1",
            Port = int.TryParse(config["Postgres:Port"], out var p) ? p : 5432,
            Database = config["Postgres:Database"] ?? "coverage_v2",
            Username = config["Postgres:Username"] ?? "coverage_app",
            // Pooling defaults are fine; keep the app resilient to short DB blips.
            Timeout = 15,
            CommandTimeout = 30,
        };

        var pw = config["Postgres:Password"];
        if (string.IsNullOrEmpty(pw))
        {
            var pwFile = config["Postgres:PasswordFile"];
            if (!string.IsNullOrWhiteSpace(pwFile) && File.Exists(pwFile))
                pw = File.ReadAllText(pwFile).Trim();
        }
        if (!string.IsNullOrEmpty(pw)) b.Password = pw;
        return b.ConnectionString;
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var c = new NpgsqlConnection(_cs);
        await c.OpenAsync().ConfigureAwait(false);
        return c;
    }

    // Normalize a DateTime to UTC for a timestamptz parameter.
    private static DateTime U(DateTime x) => x.Kind switch
    {
        DateTimeKind.Utc => x,
        DateTimeKind.Local => x.ToUniversalTime(),
        _ => DateTime.SpecifyKind(x, DateTimeKind.Utc),
    };
    private static object Un(DateTime? x) => x.HasValue ? U(x.Value) : DBNull.Value;
    // date column binding: 'yyyy-MM-dd' string, cast ::date in SQL (tz-safe).
    private static object Dn(DateTime? x) => x.HasValue ? x.Value.ToString("yyyy-MM-dd") : (object)DBNull.Value;

    // ===================== Symbol mappings =====================

    public async Task<List<SymbolMapping>> GetMappingsAsync()
    {
        try
        {
            await using var c = await OpenAsync();
            var rows = await c.QueryAsync<SymbolMapping>(
                "SELECT * FROM symbol_mappings WHERE is_active = true");
            return rows.ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetMappingsAsync failed"); return new(); }
    }

    public async Task<SymbolMapping?> UpsertMappingAsync(SymbolMapping mapping)
    {
        try
        {
            await using var c = await OpenAsync();
            const string sql = @"
INSERT INTO symbol_mappings (id, canonical_name, bbook_symbol, bbook_contract_size,
    coverage_symbol, coverage_contract_size, digits, profit_currency, is_active, pip_size)
VALUES (@Id, @CanonicalName, @BBookSymbol, @BBookContractSize,
    @CoverageSymbol, @CoverageContractSize, @Digits, @ProfitCurrency, @IsActive, @PipSize)
ON CONFLICT (id) DO UPDATE SET
    canonical_name = EXCLUDED.canonical_name, bbook_symbol = EXCLUDED.bbook_symbol,
    bbook_contract_size = EXCLUDED.bbook_contract_size, coverage_symbol = EXCLUDED.coverage_symbol,
    coverage_contract_size = EXCLUDED.coverage_contract_size, digits = EXCLUDED.digits,
    profit_currency = EXCLUDED.profit_currency, is_active = EXCLUDED.is_active, pip_size = EXCLUDED.pip_size
RETURNING *;";
            return await c.QueryFirstOrDefaultAsync<SymbolMapping>(sql, mapping);
        }
        catch (Exception ex) { _logger.LogError(ex, "UpsertMappingAsync failed"); return null; }
    }

    public async Task<bool> DeleteMappingAsync(Guid id)
    {
        try
        {
            await using var c = await OpenAsync();
            await c.ExecuteAsync("DELETE FROM symbol_mappings WHERE id = @id", new { id });
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteMappingAsync {Id} failed", id); return false; }
    }

    // ===================== Account settings =====================

    public async Task<List<AccountSettings>> GetAccountSettingsAsync()
    {
        try
        {
            await using var c = await OpenAsync();
            var rows = await c.QueryAsync<AccountSettings>(
                "SELECT * FROM account_settings ORDER BY account_type, created_at");
            return rows.ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetAccountSettingsAsync failed"); return new(); }
    }

    public async Task<AccountSettings?> CreateAccountSettingsAsync(AccountSettings s)
    {
        try
        {
            await using var c = await OpenAsync();
            const string sql = @"
INSERT INTO account_settings (id, account_type, label, server, login, password, group_mask, is_active)
VALUES (@Id, @AccountType, @Label, @Server, @Login, @Password, @GroupMask, @IsActive)
RETURNING *;";
            return await c.QueryFirstOrDefaultAsync<AccountSettings>(sql, s);
        }
        catch (Exception ex) { _logger.LogError(ex, "CreateAccountSettingsAsync failed"); return null; }
    }

    public async Task<AccountSettings?> UpdateAccountSettingsAsync(Guid id, AccountSettings s)
    {
        try
        {
            await using var c = await OpenAsync();
            const string sql = @"
UPDATE account_settings SET account_type=@AccountType, label=@Label, server=@Server,
    login=@Login, password=@Password, group_mask=@GroupMask, is_active=@IsActive
WHERE id=@id RETURNING *;";
            return await c.QueryFirstOrDefaultAsync<AccountSettings>(sql,
                new { id, s.AccountType, s.Label, s.Server, s.Login, s.Password, s.GroupMask, s.IsActive });
        }
        catch (Exception ex) { _logger.LogError(ex, "UpdateAccountSettingsAsync failed"); return null; }
    }

    public async Task<bool> DeleteAccountSettingsAsync(Guid id)
    {
        try
        {
            await using var c = await OpenAsync();
            await c.ExecuteAsync("DELETE FROM account_settings WHERE id=@id", new { id });
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteAccountSettingsAsync {Id} failed", id); return false; }
    }

    // ===================== Bridge settings =====================

    public async Task<BridgeSettings?> GetBridgeSettingsAsync()
    {
        try
        {
            await using var c = await OpenAsync();
            return await c.QueryFirstOrDefaultAsync<BridgeSettings>(
                "SELECT * FROM bridge_settings LIMIT 1");
        }
        catch (Exception ex) { _logger.LogError(ex, "GetBridgeSettingsAsync failed"); return null; }
    }

    public async Task<BridgeSettings?> UpsertBridgeSettingsAsync(BridgeSettings s)
    {
        try
        {
            await using var c = await OpenAsync();
            var existingId = await c.QueryFirstOrDefaultAsync<Guid?>("SELECT id FROM bridge_settings LIMIT 1");
            if (existingId is Guid id)
            {
                const string upd = @"
UPDATE bridge_settings SET enabled=@Enabled, mode=@Mode, base_url=@BaseUrl, client_code=@ClientCode,
    username=@Username, password=@Password, notes=@Notes, updated_at=now()
WHERE id=@id RETURNING *;";
                return await c.QueryFirstOrDefaultAsync<BridgeSettings>(upd,
                    new { id, s.Enabled, s.Mode, s.BaseUrl, s.ClientCode, s.Username, s.Password, s.Notes });
            }
            const string ins = @"
INSERT INTO bridge_settings (enabled, mode, base_url, client_code, username, password, notes)
VALUES (@Enabled, @Mode, @BaseUrl, @ClientCode, @Username, @Password, @Notes) RETURNING *;";
            return await c.QueryFirstOrDefaultAsync<BridgeSettings>(ins, s);
        }
        catch (Exception ex) { _logger.LogError(ex, "UpsertBridgeSettingsAsync failed"); return null; }
    }

    // ===================== Trading accounts =====================

    public async Task<List<TradingAccount>> GetTradingAccountsAsync(string? source = null)
    {
        try
        {
            await using var c = await OpenAsync();
            var sql = "SELECT * FROM trading_accounts" +
                      (source != null ? " WHERE source = @source" : "") + " ORDER BY login";
            var rows = await c.QueryAsync<TradingAccount>(sql, new { source });
            return rows.ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetTradingAccountsAsync failed"); return new(); }
    }

    public async Task<int> UpsertTradingAccountsAsync(IEnumerable<TradingAccount> accounts)
    {
        var list = accounts.ToList();
        if (list.Count == 0) return 0;
        try
        {
            await using var c = await OpenAsync();
            const string sql = @"
INSERT INTO trading_accounts (source, login, name, group_name, leverage, balance, equity, credit,
    margin, free_margin, currency, registration_time, last_trade_time, status, comment, synced_at, updated_at)
VALUES (@Source,@Login,@Name,@GroupName,@Leverage,@Balance,@Equity,@Credit,@Margin,@FreeMargin,
    @Currency,@RegistrationTime,@LastTradeTime,@Status,@Comment,@SyncedAt,@UpdatedAt)
ON CONFLICT (source, login) DO UPDATE SET
    name=EXCLUDED.name, group_name=EXCLUDED.group_name, leverage=EXCLUDED.leverage,
    balance=EXCLUDED.balance, equity=EXCLUDED.equity, credit=EXCLUDED.credit, margin=EXCLUDED.margin,
    free_margin=EXCLUDED.free_margin, currency=EXCLUDED.currency, registration_time=EXCLUDED.registration_time,
    last_trade_time=EXCLUDED.last_trade_time, status=EXCLUDED.status, comment=EXCLUDED.comment,
    synced_at=EXCLUDED.synced_at, updated_at=EXCLUDED.updated_at;";
            var prms = list.Select(a => new
            {
                a.Source, a.Login, a.Name, a.GroupName, a.Leverage, a.Balance, a.Equity, a.Credit,
                a.Margin, a.FreeMargin, a.Currency,
                RegistrationTime = Un(a.RegistrationTime), LastTradeTime = Un(a.LastTradeTime),
                a.Status, a.Comment, SyncedAt = U(a.SyncedAt), UpdatedAt = U(a.UpdatedAt)
            });
            await c.ExecuteAsync(sql, prms);
            return list.Count;
        }
        catch (Exception ex) { _logger.LogError(ex, "UpsertTradingAccountsAsync failed"); return 0; }
    }

    // ===================== Moved accounts =====================

    private HashSet<long> _movedLogins = new();
    private DateTime _movedLoginsLastRefresh = DateTime.MinValue;

    public async Task<HashSet<long>> GetMovedLoginsAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _movedLogins.Count > 0 && (DateTime.UtcNow - _movedLoginsLastRefresh).TotalMinutes < 5)
            return _movedLogins;
        try
        {
            await using var c = await OpenAsync();
            var rows = await c.QueryAsync<long>("SELECT login FROM moved_accounts");
            _movedLogins = new HashSet<long>(rows);
            _movedLoginsLastRefresh = DateTime.UtcNow;
        }
        catch (Exception ex) { _logger.LogError(ex, "GetMovedLoginsAsync failed"); }
        return _movedLogins;
    }

    public async Task<List<Dictionary<string, object>>> GetMovedAccountsAsync()
    {
        try
        {
            await using var c = await OpenAsync();
            var rows = await c.QueryAsync("SELECT login, moved_at, reason, moved_by FROM moved_accounts ORDER BY moved_at DESC");
            var result = new List<Dictionary<string, object>>();
            foreach (IDictionary<string, object> r in rows)
                result.Add(new Dictionary<string, object>(r));
            return result;
        }
        catch (Exception ex) { _logger.LogError(ex, "GetMovedAccountsAsync failed"); return new(); }
    }

    // ===================== Deals =====================

    public async Task<List<DealRecord>> GetNonTradeDealsAsync(string source, DateTime from, DateTime to)
    {
        try
        {
            await using var c = await OpenAsync();
            var rows = await c.QueryAsync<DealRecord>(
                @"SELECT * FROM deals WHERE source=@source AND action >= 2
                   AND deal_time >= @from AND deal_time < @to ORDER BY deal_time ASC",
                new { source, from = U(from), to = U(to) });
            return rows.ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetNonTradeDealsAsync failed"); return new(); }
    }

    public async Task<List<DealRecord>> GetTradeDealsForLoginsAsync(
        string source, IEnumerable<long> logins, DateTime fromUtc, DateTime toUtc)
    {
        var arr = logins.Distinct().ToArray();
        if (arr.Length == 0) return new();
        try
        {
            await using var c = await OpenAsync();
            var rows = await c.QueryAsync<DealRecord>(
                @"SELECT * FROM deals WHERE source=@source AND action < 2 AND login = ANY(@logins)
                   AND deal_time >= @from AND deal_time < @to ORDER BY deal_time ASC",
                new { source, logins = arr, from = U(fromUtc), to = U(toUtc) });
            return rows.ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetTradeDealsForLoginsAsync failed"); return new(); }
    }

    public async Task<Dictionary<long, decimal>> SumTradeBalanceFlowPerLoginAsync(
        string source, DateTime fromUtc, DateTime toUtc)
    {
        var totals = new Dictionary<long, decimal>();
        try
        {
            await using var c = await OpenAsync();
            // Server-side aggregation (v1 paged 1000 rows/call and summed in C#).
            // "action NOT IN (2,3)" matches v1: everything except BALANCE/CREDIT.
            var rows = await c.QueryAsync(
                @"SELECT login, SUM(profit + commission + swap + fee) AS flow
                    FROM deals
                   WHERE source=@source AND action NOT IN (2,3)
                     AND deal_time >= @from AND deal_time < @to
                   GROUP BY login",
                new { source, from = U(fromUtc), to = U(toUtc) });
            foreach (var r in rows) totals[(long)r.login] = (decimal)r.flow;
        }
        catch (Exception ex) { _logger.LogError(ex, "SumTradeBalanceFlowPerLoginAsync failed"); }
        return totals;
    }

    public async Task<List<DealRecord>> GetDealsAsync(string source, DateTime from, DateTime to)
    {
        try
        {
            await using var c = await OpenAsync();
            var rows = await c.QueryAsync<DealRecord>(
                @"SELECT * FROM deals WHERE source=@source
                   AND deal_time >= @from AND deal_time <= @to ORDER BY deal_time",
                new { source, from = U(from), to = U(to) });
            return rows.ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetDealsAsync failed"); return new(); }
    }

    public async Task<DateTime?> GetLastDealTimeAsync(string source)
    {
        try
        {
            await using var c = await OpenAsync();
            return await c.QueryFirstOrDefaultAsync<DateTime?>(
                "SELECT max(deal_time) FROM deals WHERE source=@source", new { source });
        }
        catch (Exception ex) { _logger.LogError(ex, "GetLastDealTimeAsync failed"); return null; }
    }

    public async Task<int> DeleteDealsAsync(string source, IEnumerable<long> dealIds)
    {
        var ids = dealIds.Distinct().ToArray();
        if (ids.Length == 0) return 0;
        try
        {
            await using var c = await OpenAsync();
            return await c.ExecuteAsync(
                "DELETE FROM deals WHERE source=@source AND deal_id = ANY(@ids)", new { source, ids });
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteDealsAsync failed"); return 0; }
    }

    public async Task<int> UpsertDealsAsync(IEnumerable<DealRecord> deals)
    {
        var list = deals.ToList();
        if (list.Count == 0) return 0;
        try
        {
            await using var c = await OpenAsync();
            const string sql = @"
INSERT INTO deals (source, deal_id, login, symbol, canonical_symbol, direction, action, entry,
    volume, price, profit, commission, swap, fee, order_id, position_id, deal_time)
VALUES (@Source,@DealId,@Login,@Symbol,@CanonicalSymbol,@Direction,@Action,@Entry,
    @Volume,@Price,@Profit,@Commission,@Swap,@Fee,@OrderId,@PositionId,@DealTime)
ON CONFLICT (source, deal_id) DO UPDATE SET
    login=EXCLUDED.login, symbol=EXCLUDED.symbol, canonical_symbol=EXCLUDED.canonical_symbol,
    direction=EXCLUDED.direction, action=EXCLUDED.action, entry=EXCLUDED.entry, volume=EXCLUDED.volume,
    price=EXCLUDED.price, profit=EXCLUDED.profit, commission=EXCLUDED.commission, swap=EXCLUDED.swap,
    fee=EXCLUDED.fee, order_id=EXCLUDED.order_id, position_id=EXCLUDED.position_id, deal_time=EXCLUDED.deal_time;";
            var prms = list.Select(d => new
            {
                d.Source, d.DealId, d.Login, d.Symbol, d.CanonicalSymbol, d.Direction, d.Action, d.Entry,
                d.Volume, d.Price, d.Profit, d.Commission, d.Swap, d.Fee, d.OrderId, d.PositionId,
                DealTime = U(d.DealTime)
            });
            await c.ExecuteAsync(sql, prms);
            return list.Count;
        }
        catch (Exception ex) { _logger.LogError(ex, "UpsertDealsAsync failed"); return 0; }
    }

    public async Task<int> DetectAndLogDealChangesAsync(IEnumerable<DealRecord> incomingDeals, string source)
    {
        try
        {
            var incoming = incomingDeals.ToList();
            if (incoming.Count == 0) return 0;
            var ids = incoming.Select(d => d.DealId).Distinct().ToArray();

            await using var c = await OpenAsync();
            var existingRows = await c.QueryAsync<DealRecord>(
                "SELECT * FROM deals WHERE source=@source AND deal_id = ANY(@ids)", new { source, ids });
            var existing = existingRows.ToDictionary(r => r.DealId);

            var auditEntries = new List<TradeAuditEntry>();
            foreach (var deal in incoming)
            {
                if (!existing.TryGetValue(deal.DealId, out var old)) continue;
                void Check(string field, string? oldVal, string? newVal)
                {
                    if (oldVal != newVal)
                        auditEntries.Add(new TradeAuditEntry
                        {
                            Source = source, DealId = deal.DealId, PositionId = deal.PositionId,
                            Login = deal.Login, Symbol = deal.Symbol, FieldChanged = field,
                            OldValue = oldVal, NewValue = newVal, ChangeType = "modified"
                        });
                }
                Check("price", old.Price.ToString("F5"), deal.Price.ToString("F5"));
                Check("volume", old.Volume.ToString("F2"), deal.Volume.ToString("F2"));
                Check("profit", old.Profit.ToString("F2"), deal.Profit.ToString("F2"));
                Check("commission", old.Commission.ToString("F2"), deal.Commission.ToString("F2"));
                Check("swap", old.Swap.ToString("F2"), deal.Swap.ToString("F2"));
                Check("fee", old.Fee.ToString("F2"), deal.Fee.ToString("F2"));
                Check("direction", old.Direction, deal.Direction);
                Check("entry", old.Entry.ToString(), deal.Entry.ToString());
            }

            if (auditEntries.Count > 0)
            {
                await InsertAuditEntriesAsync(auditEntries);
                _logger.LogWarning("Detected {Count} deal modifications for source={Source}", auditEntries.Count, source);
            }
            return auditEntries.Count;
        }
        catch (Exception ex) { _logger.LogError(ex, "DetectAndLogDealChangesAsync failed"); return 0; }
    }

    // ===================== Audit log =====================

    public async Task InsertAuditEntriesAsync(IEnumerable<TradeAuditEntry> entries)
    {
        var list = entries.ToList();
        if (list.Count == 0) return;
        try
        {
            await using var c = await OpenAsync();
            const string sql = @"
INSERT INTO trade_audit_log (source, deal_id, position_id, login, symbol, field_changed,
    old_value, new_value, changed_by, change_type, detected_at)
VALUES (@Source,@DealId,@PositionId,@Login,@Symbol,@FieldChanged,@OldValue,@NewValue,@ChangedBy,@ChangeType,@DetectedAt);";
            var prms = list.Select(e => new
            {
                e.Source, e.DealId, e.PositionId, e.Login, e.Symbol, e.FieldChanged,
                e.OldValue, e.NewValue, e.ChangedBy, e.ChangeType, DetectedAt = U(e.DetectedAt)
            });
            await c.ExecuteAsync(sql, prms);
        }
        catch (Exception ex) { _logger.LogError(ex, "InsertAuditEntriesAsync failed"); }
    }

    public async Task<List<TradeAuditEntry>> GetAuditLogAsync(DateTime? from = null, string? symbol = null, long? login = null)
    {
        try
        {
            await using var c = await OpenAsync();
            var wh = new List<string>();
            if (from != null) wh.Add("detected_at >= @from");
            if (symbol != null) wh.Add("symbol = @symbol");
            if (login != null) wh.Add("login = @login");
            var sql = "SELECT * FROM trade_audit_log" +
                      (wh.Count > 0 ? " WHERE " + string.Join(" AND ", wh) : "") +
                      " ORDER BY detected_at DESC LIMIT 500";
            var rows = await c.QueryAsync<TradeAuditEntry>(sql,
                new { from = from.HasValue ? U(from.Value) : (object)DBNull.Value, symbol, login });
            return rows.ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetAuditLogAsync failed"); return new(); }
    }

    // ===================== Alert rules =====================

    public async Task<List<RiskThreshold>> GetAlertRulesAsync()
    {
        try
        {
            await using var c = await OpenAsync();
            var rows = await c.QueryAsync<RiskThreshold>("SELECT * FROM alert_rules ORDER BY created_at");
            return rows.ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetAlertRulesAsync failed"); return new(); }
    }

    public async Task<RiskThreshold?> UpsertAlertRuleAsync(RiskThreshold rule)
    {
        try
        {
            await using var c = await OpenAsync();
            if (rule.Id.HasValue)
            {
                const string sql = @"
INSERT INTO alert_rules (id, symbol, trigger_type, operator, value, severity, enabled, updated_at)
VALUES (@Id,@Symbol,@TriggerType,@Operator,@Value,@Severity,@Enabled, now())
ON CONFLICT (id) DO UPDATE SET symbol=EXCLUDED.symbol, trigger_type=EXCLUDED.trigger_type,
    operator=EXCLUDED.operator, value=EXCLUDED.value, severity=EXCLUDED.severity,
    enabled=EXCLUDED.enabled, updated_at=now()
RETURNING *;";
                return await c.QueryFirstOrDefaultAsync<RiskThreshold>(sql, rule);
            }
            const string ins = @"
INSERT INTO alert_rules (symbol, trigger_type, operator, value, severity, enabled)
VALUES (@Symbol,@TriggerType,@Operator,@Value,@Severity,@Enabled) RETURNING *;";
            return await c.QueryFirstOrDefaultAsync<RiskThreshold>(ins, rule);
        }
        catch (Exception ex) { _logger.LogError(ex, "UpsertAlertRuleAsync failed"); return null; }
    }

    public async Task<bool> DeleteAlertRuleAsync(Guid id)
    {
        try
        {
            await using var c = await OpenAsync();
            await c.ExecuteAsync("DELETE FROM alert_rules WHERE id=@id", new { id });
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteAlertRuleAsync {Id} failed", id); return false; }
    }

    // ===================== Alert events =====================

    public async Task<int> InsertAlertEventsAsync(IEnumerable<AlertEvent> events)
    {
        var list = events.ToList();
        if (list.Count == 0) return 0;
        try
        {
            await using var c = await OpenAsync();
            const string sql = @"
INSERT INTO alert_events (id, threshold_id, trigger_type, symbol, severity, message,
    threshold_value, actual_value, triggered_at, acknowledged, acknowledged_at)
VALUES (@Id,@ThresholdId,@TriggerType,@Symbol,@Severity,@Message,@ThresholdValue,@ActualValue,
    @TriggeredAt,@Acknowledged,@AcknowledgedAt);";
            var prms = list.Select(e => new
            {
                e.Id, e.ThresholdId, e.TriggerType, e.Symbol, e.Severity, e.Message,
                e.ThresholdValue, e.ActualValue, TriggeredAt = U(e.TriggeredAt),
                e.Acknowledged, AcknowledgedAt = Un(e.AcknowledgedAt)
            });
            await c.ExecuteAsync(sql, prms);
            return list.Count;
        }
        catch (Exception ex) { _logger.LogError(ex, "InsertAlertEventsAsync failed"); return 0; }
    }

    public async Task<List<AlertEvent>> GetAlertEventsAsync(bool unacknowledgedOnly = false, int limit = 100)
    {
        try
        {
            await using var c = await OpenAsync();
            var sql = "SELECT * FROM alert_events" +
                      (unacknowledgedOnly ? " WHERE acknowledged = false" : "") +
                      " ORDER BY triggered_at DESC LIMIT @limit";
            var rows = await c.QueryAsync<AlertEvent>(sql, new { limit });
            return rows.ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetAlertEventsAsync failed"); return new(); }
    }

    public async Task<bool> AcknowledgeAlertEventAsync(Guid id)
    {
        try
        {
            await using var c = await OpenAsync();
            await c.ExecuteAsync(
                "UPDATE alert_events SET acknowledged=true, acknowledged_at=now() WHERE id=@id", new { id });
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "AcknowledgeAlertEventAsync {Id} failed", id); return false; }
    }

    // ===================== Exposure snapshots =====================

    public async Task<int> UpsertExposureSnapshotsAsync(IEnumerable<ExposureSnapshot> snapshots)
    {
        var batch = snapshots.ToList();
        if (batch.Count == 0) return 0;
        try
        {
            await using var c = await OpenAsync();
            const string sql = @"
INSERT INTO exposure_snapshots (canonical_symbol, snapshot_time, bbook_buy_volume, bbook_sell_volume,
    coverage_buy_volume, coverage_sell_volume, net_volume, bbook_pnl, coverage_pnl, net_pnl, trigger_type, label)
VALUES (@CanonicalSymbol,@SnapshotTime,@BBookBuyVolume,@BBookSellVolume,@CoverageBuyVolume,
    @CoverageSellVolume,@NetVolume,@BBookPnL,@CoveragePnL,@NetPnL,@TriggerType,@Label)
ON CONFLICT (canonical_symbol, snapshot_time) DO UPDATE SET
    bbook_buy_volume=EXCLUDED.bbook_buy_volume, bbook_sell_volume=EXCLUDED.bbook_sell_volume,
    coverage_buy_volume=EXCLUDED.coverage_buy_volume, coverage_sell_volume=EXCLUDED.coverage_sell_volume,
    net_volume=EXCLUDED.net_volume, bbook_pnl=EXCLUDED.bbook_pnl, coverage_pnl=EXCLUDED.coverage_pnl,
    net_pnl=EXCLUDED.net_pnl, trigger_type=EXCLUDED.trigger_type, label=EXCLUDED.label;";
            var prms = batch.Select(s => new
            {
                s.CanonicalSymbol, SnapshotTime = U(s.SnapshotTime), s.BBookBuyVolume, s.BBookSellVolume,
                s.CoverageBuyVolume, s.CoverageSellVolume, s.NetVolume, s.BBookPnL, s.CoveragePnL, s.NetPnL,
                s.TriggerType, s.Label
            });
            await c.ExecuteAsync(sql, prms);
            return batch.Count;
        }
        catch (Exception ex) { _logger.LogError(ex, "UpsertExposureSnapshotsAsync failed"); return 0; }
    }

    public async Task<Dictionary<string, ExposureSnapshot>> GetNearestSnapshotsBeforeAsync(DateTime anchorUtc)
    {
        try
        {
            await using var c = await OpenAsync();
            var rows = await c.QueryAsync<ExposureSnapshot>(
                "SELECT * FROM latest_snapshots_before(@anchor)", new { anchor = U(anchorUtc) });
            var result = new Dictionary<string, ExposureSnapshot>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in rows) result[r.CanonicalSymbol.ToUpperInvariant()] = r;
            return result;
        }
        catch (Exception ex) { _logger.LogError(ex, "GetNearestSnapshotsBeforeAsync failed"); return new(); }
    }

    public async Task<Dictionary<string, ExposureSnapshot>> GetSnapshotsAtAsync(DateTime exactSnapshotTimeUtc)
    {
        try
        {
            await using var c = await OpenAsync();
            var rows = await c.QueryAsync<ExposureSnapshot>(
                "SELECT * FROM exposure_snapshots WHERE snapshot_time = @ts LIMIT 2000",
                new { ts = U(exactSnapshotTimeUtc) });
            var result = new Dictionary<string, ExposureSnapshot>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in rows) result[r.CanonicalSymbol.ToUpperInvariant()] = r;
            return result;
        }
        catch (Exception ex) { _logger.LogError(ex, "GetSnapshotsAtAsync failed"); return new(); }
    }

    public async Task<List<ExposureSnapshot>> ListExposureSnapshotsAsync(DateTime fromUtc, DateTime toUtc, string? canonicalSymbol = null)
    {
        try
        {
            await using var c = await OpenAsync();
            var sql = "SELECT * FROM exposure_snapshots WHERE snapshot_time >= @from AND snapshot_time <= @to" +
                      (!string.IsNullOrEmpty(canonicalSymbol) ? " AND canonical_symbol = @sym" : "") +
                      " ORDER BY snapshot_time DESC LIMIT 2000";
            var rows = await c.QueryAsync<ExposureSnapshot>(sql,
                new { from = U(fromUtc), to = U(toUtc), sym = canonicalSymbol });
            return rows.ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "ListExposureSnapshotsAsync failed"); return new(); }
    }

    // ===================== Aggregation RPCs (migration 0090) =====================

    public async Task<Dictionary<string, decimal>> AggregateBBookSettledPnlAsync(
        DateTime fromUtc, DateTime toUtc, IEnumerable<long> excludedLogins)
    {
        var result = new Dictionary<string, decimal>(StringComparer.Ordinal);
        try
        {
            await using var c = await OpenAsync();
            var rows = await c.QueryAsync(
                "SELECT canonical_key, net_pnl FROM aggregate_bbook_settled_pnl(@from, @to, @excl)",
                new { from = U(fromUtc), to = U(toUtc), excl = excludedLogins.ToArray() });
            foreach (var r in rows)
            {
                var key = (string?)r.canonical_key ?? "";
                if (!string.IsNullOrEmpty(key)) result[key] = (decimal)r.net_pnl;
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "AggregateBBookSettledPnlAsync failed"); }
        return result;
    }

    public async Task<List<SymbolPnL>> AggregateBBookPnLFullAsync(
        DateTime fromUtc, DateTime toUtc, IEnumerable<long> excludedLogins)
    {
        try
        {
            await using var c = await OpenAsync();
            var rows = await c.QueryAsync<SymbolPnL>(
                @"SELECT symbol, deal_count::int AS deal_count, total_profit, total_commission, total_swap,
                         total_fee, total_volume, buy_volume, sell_volume
                    FROM aggregate_bbook_pnl_full(@from, @to, @excl)",
                new { from = U(fromUtc), to = U(toUtc), excl = excludedLogins.ToArray() });
            return rows.ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "AggregateBBookPnLFullAsync failed"); return new(); }
    }

    // ===================== Reconciliation runs =====================

    public async Task<ReconciliationRun?> InsertReconciliationRunAsync(ReconciliationRun run)
    {
        try
        {
            await using var c = await OpenAsync();
            const string sql = @"
INSERT INTO reconciliation_runs (trigger_type, window_from, window_to, started_at, finished_at,
    mt5_deal_count, supabase_deal_count, backfilled, ghost_deleted, modified, error, notes)
VALUES (@TriggerType,@WindowFrom,@WindowTo,@StartedAt,@FinishedAt,@Mt5DealCount,@SupabaseDealCount,
    @Backfilled,@GhostDeleted,@Modified,@Error,@Notes) RETURNING *;";
            return await c.QueryFirstOrDefaultAsync<ReconciliationRun>(sql, new
            {
                run.TriggerType, WindowFrom = U(run.WindowFrom), WindowTo = U(run.WindowTo),
                StartedAt = U(run.StartedAt), FinishedAt = Un(run.FinishedAt),
                run.Mt5DealCount, run.SupabaseDealCount, run.Backfilled, run.GhostDeleted, run.Modified,
                run.Error, run.Notes
            });
        }
        catch (Exception ex) { _logger.LogError(ex, "InsertReconciliationRunAsync failed"); return null; }
    }

    public async Task<List<ReconciliationRun>> ListReconciliationRunsAsync(int limit = 50)
    {
        try
        {
            await using var c = await OpenAsync();
            var rows = await c.QueryAsync<ReconciliationRun>(
                "SELECT * FROM reconciliation_runs ORDER BY started_at DESC LIMIT @limit",
                new { limit = Math.Clamp(limit, 1, 500) });
            return rows.ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "ListReconciliationRunsAsync failed"); return new(); }
    }

    // ===================== Snapshot schedules =====================

    public async Task<List<SnapshotSchedule>> GetSnapshotSchedulesAsync()
    {
        try
        {
            await using var c = await OpenAsync();
            var rows = await c.QueryAsync<SnapshotSchedule>("SELECT * FROM snapshot_schedules ORDER BY name ASC");
            return rows.ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetSnapshotSchedulesAsync failed"); return new(); }
    }

    public async Task<SnapshotSchedule?> UpsertSnapshotScheduleAsync(SnapshotSchedule s)
    {
        try
        {
            await using var c = await OpenAsync();
            if (s.Id.HasValue && s.Id.Value != Guid.Empty)
            {
                const string upd = @"
UPDATE snapshot_schedules SET name=@Name, cadence=@Cadence, cron_expr=@CronExpr, tz=@Tz, enabled=@Enabled,
    last_run_at=@LastRunAt, next_run_at=@NextRunAt, updated_at=now()
WHERE id=@Id RETURNING *;";
                return await c.QueryFirstOrDefaultAsync<SnapshotSchedule>(upd, new
                {
                    s.Id, s.Name, s.Cadence, s.CronExpr, s.Tz, s.Enabled,
                    LastRunAt = Un(s.LastRunAt), NextRunAt = Un(s.NextRunAt)
                });
            }
            const string ins = @"
INSERT INTO snapshot_schedules (name, cadence, cron_expr, tz, enabled, last_run_at, next_run_at)
VALUES (@Name,@Cadence,@CronExpr,@Tz,@Enabled,@LastRunAt,@NextRunAt) RETURNING *;";
            return await c.QueryFirstOrDefaultAsync<SnapshotSchedule>(ins, new
            {
                s.Name, s.Cadence, s.CronExpr, s.Tz, s.Enabled,
                LastRunAt = Un(s.LastRunAt), NextRunAt = Un(s.NextRunAt)
            });
        }
        catch (Exception ex) { _logger.LogError(ex, "UpsertSnapshotScheduleAsync failed"); return null; }
    }

    public async Task<bool> DeleteSnapshotScheduleAsync(Guid id)
    {
        try
        {
            await using var c = await OpenAsync();
            await c.ExecuteAsync("DELETE FROM snapshot_schedules WHERE id=@id", new { id });
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteSnapshotScheduleAsync {Id} failed", id); return false; }
    }

    // ===================== Equity P&L: account equity snapshots =====================

    public async Task<int> UpsertAccountEquitySnapshotsAsync(IEnumerable<AccountEquitySnapshot> rows)
    {
        var batch = rows.ToList();
        if (batch.Count == 0) return 0;
        try
        {
            await using var c = await OpenAsync();
            const string sql = @"
INSERT INTO account_equity_snapshots (login, source, snapshot_time, balance, equity, credit, margin, trigger_type, label)
VALUES (@Login,@Source,@SnapshotTime,@Balance,@Equity,@Credit,@Margin,@TriggerType,@Label)
ON CONFLICT (login, source, snapshot_time) DO UPDATE SET
    balance=EXCLUDED.balance, equity=EXCLUDED.equity, credit=EXCLUDED.credit, margin=EXCLUDED.margin,
    trigger_type=EXCLUDED.trigger_type, label=EXCLUDED.label;";
            var prms = batch.Select(r => new
            {
                r.Login, r.Source, SnapshotTime = U(r.SnapshotTime), r.Balance, r.Equity, r.Credit,
                Margin = r.Margin.HasValue ? (object)r.Margin.Value : DBNull.Value, r.TriggerType, r.Label
            });
            await c.ExecuteAsync(sql, prms);
            return batch.Count;
        }
        catch (Exception ex) { _logger.LogError(ex, "UpsertAccountEquitySnapshotsAsync failed"); return 0; }
    }

    public async Task<Dictionary<string, AccountEquitySnapshot>> GetAccountEquitySnapshotsBeforeAsync(DateTime anchorUtc)
    {
        try
        {
            await using var c = await OpenAsync();
            // Latest row per (login, source) at or before the anchor.
            var rows = await c.QueryAsync<AccountEquitySnapshot>(
                @"SELECT DISTINCT ON (login, source) *
                    FROM account_equity_snapshots
                   WHERE snapshot_time <= @anchor
                   ORDER BY login, source, snapshot_time DESC",
                new { anchor = U(anchorUtc) });
            var result = new Dictionary<string, AccountEquitySnapshot>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in rows) result[$"{r.Source}:{r.Login}"] = r;
            return result;
        }
        catch (Exception ex) { _logger.LogError(ex, "GetAccountEquitySnapshotsBeforeAsync failed"); return new(); }
    }

    public async Task<List<AccountEquitySnapshot>> GetAccountEquitySnapshotsInRangeAsync(
        long login, string source, DateTime fromUtc, DateTime toUtc)
    {
        try
        {
            await using var c = await OpenAsync();
            var rows = await c.QueryAsync<AccountEquitySnapshot>(
                @"SELECT * FROM account_equity_snapshots
                   WHERE login=@login AND source=@source
                     AND snapshot_time >= @from AND snapshot_time <= @to
                   ORDER BY snapshot_time ASC",
                new { login, source, from = U(fromUtc), to = U(toUtc) });
            return rows.ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetAccountEquitySnapshotsInRangeAsync failed"); return new(); }
    }

    // ===================== Equity P&L: client config =====================

    public async Task<List<EquityPnLClientConfig>> GetEquityPnLClientConfigsAsync(string? source = null)
    {
        try
        {
            await using var c = await OpenAsync();
            var sql = "SELECT * FROM equity_pnl_client_config" +
                      (source != null ? " WHERE source=@source" : "") + " ORDER BY login";
            var rows = await c.QueryAsync<EquityPnLClientConfig>(sql, new { source });
            return rows.ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetEquityPnLClientConfigsAsync failed"); return new(); }
    }

    private const string ClientConfigUpsertSql = @"
INSERT INTO equity_pnl_client_config (login, source, comm_rebate_pct, ps_pct, ps_contract_start,
    ps_cum_pl, ps_low_water_mark, ps_last_processed_month, notes, updated_at)
VALUES (@Login,@Source,@CommRebatePct,@PsPct,@PsContractStart::date,@PsCumPl,@PsLowWaterMark,
    @PsLastProcessedMonth::date,@Notes, now())
ON CONFLICT (login, source) DO UPDATE SET
    comm_rebate_pct=EXCLUDED.comm_rebate_pct, ps_pct=EXCLUDED.ps_pct, ps_contract_start=EXCLUDED.ps_contract_start,
    ps_cum_pl=EXCLUDED.ps_cum_pl, ps_low_water_mark=EXCLUDED.ps_low_water_mark,
    ps_last_processed_month=EXCLUDED.ps_last_processed_month, notes=EXCLUDED.notes, updated_at=now();";

    private static object ClientConfigParams(EquityPnLClientConfig cfg) => new
    {
        cfg.Login, cfg.Source, cfg.CommRebatePct, cfg.PsPct, PsContractStart = Dn(cfg.PsContractStart),
        cfg.PsCumPl, cfg.PsLowWaterMark, PsLastProcessedMonth = Dn(cfg.PsLastProcessedMonth), cfg.Notes
    };

    public async Task<bool> UpsertEquityPnLClientConfigAsync(EquityPnLClientConfig cfg)
    {
        try
        {
            await using var c = await OpenAsync();
            await c.ExecuteAsync(ClientConfigUpsertSql, ClientConfigParams(cfg));
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "UpsertEquityPnLClientConfigAsync failed"); return false; }
    }

    public async Task<int> UpsertEquityPnLClientConfigsAsync(IEnumerable<EquityPnLClientConfig> cfgs)
    {
        var batch = cfgs.ToList();
        if (batch.Count == 0) return 0;
        try
        {
            await using var c = await OpenAsync();
            await c.ExecuteAsync(ClientConfigUpsertSql, batch.Select(ClientConfigParams));
            return batch.Count;
        }
        catch (Exception ex) { _logger.LogError(ex, "UpsertEquityPnLClientConfigsAsync failed"); return 0; }
    }

    // ===================== Equity P&L: spread rebates =====================

    public async Task<List<SpreadRebateRate>> GetSpreadRebateRatesAsync(long? login = null)
    {
        try
        {
            await using var c = await OpenAsync();
            var sql = "SELECT * FROM equity_pnl_spread_rebates" +
                      (login.HasValue ? " WHERE login=@login" : "") + " ORDER BY login, canonical_symbol";
            var rows = await c.QueryAsync<SpreadRebateRate>(sql, new { login });
            return rows.ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetSpreadRebateRatesAsync failed"); return new(); }
    }

    public async Task<int> UpsertSpreadRebateRatesAsync(IEnumerable<SpreadRebateRate> rates)
    {
        var batch = rates.ToList();
        if (batch.Count == 0) return 0;
        try
        {
            await using var c = await OpenAsync();
            const string sql = @"
INSERT INTO equity_pnl_spread_rebates (login, source, canonical_symbol, rate_per_lot, updated_at)
VALUES (@Login,@Source,@CanonicalSymbol,@RatePerLot, now())
ON CONFLICT (login, source, canonical_symbol) DO UPDATE SET
    rate_per_lot=EXCLUDED.rate_per_lot, updated_at=now();";
            await c.ExecuteAsync(sql, batch.Select(r => new { r.Login, r.Source, r.CanonicalSymbol, r.RatePerLot }));
            return batch.Count;
        }
        catch (Exception ex) { _logger.LogError(ex, "UpsertSpreadRebateRatesAsync failed"); return 0; }
    }

    public async Task<bool> DeleteSpreadRebateRateAsync(long login, string source, string canonicalSymbol)
    {
        try
        {
            await using var c = await OpenAsync();
            await c.ExecuteAsync(
                "DELETE FROM equity_pnl_spread_rebates WHERE login=@login AND source=@source AND canonical_symbol=@canonicalSymbol",
                new { login, source, canonicalSymbol });
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteSpreadRebateRateAsync failed"); return false; }
    }

    // ===================== Equity P&L Phase 2: login groups =====================

    public async Task<List<LoginGroup>> GetLoginGroupsAsync()
    {
        try
        {
            await using var c = await OpenAsync();
            var rows = await c.QueryAsync<LoginGroup>("SELECT * FROM login_groups ORDER BY name");
            return rows.ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetLoginGroupsAsync failed"); return new(); }
    }

    public async Task<LoginGroup?> UpsertLoginGroupAsync(LoginGroup g)
    {
        try
        {
            await using var c = await OpenAsync();
            if (g.Id.HasValue)
                return await c.QueryFirstOrDefaultAsync<LoginGroup>(
                    "UPDATE login_groups SET name=@Name, description=@Description, updated_at=now() WHERE id=@Id RETURNING *;",
                    new { g.Id, g.Name, g.Description });
            return await c.QueryFirstOrDefaultAsync<LoginGroup>(
                "INSERT INTO login_groups (name, description) VALUES (@Name, @Description) RETURNING *;",
                new { g.Name, g.Description });
        }
        catch (Exception ex) { _logger.LogError(ex, "UpsertLoginGroupAsync failed"); return null; }
    }

    public async Task<bool> DeleteLoginGroupAsync(Guid id)
    {
        try
        {
            await using var c = await OpenAsync();
            await c.ExecuteAsync("DELETE FROM login_groups WHERE id=@id", new { id });
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteLoginGroupAsync {Id} failed", id); return false; }
    }

    public async Task<List<LoginGroupMember>> GetLoginGroupMembersAsync(Guid? groupId = null)
    {
        try
        {
            await using var c = await OpenAsync();
            var sql = "SELECT * FROM login_group_members" +
                      (groupId.HasValue ? " WHERE group_id=@groupId" : "") +
                      " ORDER BY group_id, priority DESC, login";
            var rows = await c.QueryAsync<LoginGroupMember>(sql, new { groupId });
            return rows.ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetLoginGroupMembersAsync failed"); return new(); }
    }

    public async Task<bool> AddLoginGroupMemberAsync(LoginGroupMember m)
    {
        try
        {
            await using var c = await OpenAsync();
            await c.ExecuteAsync(
                @"INSERT INTO login_group_members (group_id, login, source, priority)
                  VALUES (@GroupId,@Login,@Source,@Priority)
                  ON CONFLICT (group_id, login, source) DO UPDATE SET priority=EXCLUDED.priority;",
                new { m.GroupId, m.Login, m.Source, m.Priority });
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "AddLoginGroupMemberAsync failed"); return false; }
    }

    public async Task<bool> RemoveLoginGroupMemberAsync(Guid groupId, long login, string source)
    {
        try
        {
            await using var c = await OpenAsync();
            await c.ExecuteAsync(
                "DELETE FROM login_group_members WHERE group_id=@groupId AND login=@login AND source=@source",
                new { groupId, login, source });
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "RemoveLoginGroupMemberAsync failed"); return false; }
    }

    public async Task<List<EquityPnLGroupConfig>> GetGroupConfigsAsync()
    {
        try
        {
            await using var c = await OpenAsync();
            var rows = await c.QueryAsync<EquityPnLGroupConfig>("SELECT * FROM equity_pnl_group_config");
            return rows.ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetGroupConfigsAsync failed"); return new(); }
    }

    public async Task<bool> UpsertGroupConfigAsync(EquityPnLGroupConfig cfg)
    {
        try
        {
            await using var c = await OpenAsync();
            await c.ExecuteAsync(
                @"INSERT INTO equity_pnl_group_config (group_id, comm_rebate_pct, ps_pct, notes, updated_at)
                  VALUES (@GroupId,@CommRebatePct,@PsPct,@Notes, now())
                  ON CONFLICT (group_id) DO UPDATE SET comm_rebate_pct=EXCLUDED.comm_rebate_pct,
                      ps_pct=EXCLUDED.ps_pct, notes=EXCLUDED.notes, updated_at=now();",
                new { cfg.GroupId, cfg.CommRebatePct, cfg.PsPct, cfg.Notes });
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "UpsertGroupConfigAsync failed"); return false; }
    }

    public async Task<List<GroupSpreadRebateRate>> GetGroupSpreadRebateRatesAsync(Guid? groupId = null)
    {
        try
        {
            await using var c = await OpenAsync();
            var sql = "SELECT * FROM equity_pnl_group_spread_rebates" +
                      (groupId.HasValue ? " WHERE group_id=@groupId" : "") +
                      " ORDER BY group_id, canonical_symbol";
            var rows = await c.QueryAsync<GroupSpreadRebateRate>(sql, new { groupId });
            return rows.ToList();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetGroupSpreadRebateRatesAsync failed"); return new(); }
    }

    public async Task<bool> UpsertGroupSpreadRebateRatesAsync(IEnumerable<GroupSpreadRebateRate> rates)
    {
        var batch = rates.ToList();
        if (batch.Count == 0) return true;
        try
        {
            await using var c = await OpenAsync();
            await c.ExecuteAsync(
                @"INSERT INTO equity_pnl_group_spread_rebates (group_id, canonical_symbol, rate_per_lot, updated_at)
                  VALUES (@GroupId,@CanonicalSymbol,@RatePerLot, now())
                  ON CONFLICT (group_id, canonical_symbol) DO UPDATE SET
                      rate_per_lot=EXCLUDED.rate_per_lot, updated_at=now();",
                batch.Select(r => new { r.GroupId, r.CanonicalSymbol, r.RatePerLot }));
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "UpsertGroupSpreadRebateRatesAsync failed"); return false; }
    }

    public async Task<bool> DeleteGroupSpreadRebateRateAsync(Guid groupId, string canonicalSymbol)
    {
        try
        {
            await using var c = await OpenAsync();
            await c.ExecuteAsync(
                "DELETE FROM equity_pnl_group_spread_rebates WHERE group_id=@groupId AND canonical_symbol=@canonicalSymbol",
                new { groupId, canonicalSymbol });
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteGroupSpreadRebateRateAsync failed"); return false; }
    }
}
