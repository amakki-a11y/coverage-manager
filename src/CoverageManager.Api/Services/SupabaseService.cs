using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CoverageManager.Core.Models;
using CoverageManager.Core.Models.Bridge;

namespace CoverageManager.Api.Services;

public class SupabaseService
{
    private readonly HttpClient _http;
    private readonly string _url;
    private readonly string _key;
    private readonly ILogger<SupabaseService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SupabaseService(IConfiguration config, HttpClient http, ILogger<SupabaseService> logger)
    {
        _http = http;
        _url = config["Supabase:Url"] ?? throw new ArgumentException("Supabase:Url not configured");
        _key = config["Supabase:Key"] ?? throw new ArgumentException("Supabase:Key not configured");
        _logger = logger;

        _http.DefaultRequestHeaders.Add("apikey", _key);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _key);
    }

    public async Task<List<SymbolMapping>> GetMappingsAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_url}/rest/v1/symbol_mappings?is_active=eq.true&select=*").ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<SymbolMapping>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch symbol mappings from Supabase");
            return [];
        }
    }

    public async Task<SymbolMapping?> UpsertMappingAsync(SymbolMapping mapping)
    {
        try
        {
            var json = JsonSerializer.Serialize(mapping, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/symbol_mappings")
            {
                Content = content
            };
            request.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");

            var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var list = JsonSerializer.Deserialize<List<SymbolMapping>>(result, JsonOptions);
            return list?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert symbol mapping");
            return null;
        }
    }

    public async Task<bool> DeleteMappingAsync(Guid id)
    {
        try
        {
            var response = await _http.DeleteAsync($"{_url}/rest/v1/symbol_mappings?id=eq.{id}").ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete symbol mapping {Id}", id);
            return false;
        }
    }

    // ── Account Settings ──

    public async Task<List<AccountSettings>> GetAccountSettingsAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_url}/rest/v1/account_settings?select=*&order=account_type,created_at").ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<AccountSettings>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch account settings from Supabase");
            return [];
        }
    }

    public async Task<AccountSettings?> CreateAccountSettingsAsync(AccountSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/account_settings")
            {
                Content = content
            };
            request.Headers.Add("Prefer", "return=representation");

            var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var list = JsonSerializer.Deserialize<List<AccountSettings>>(result, JsonOptions);
            return list?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create account settings");
            return null;
        }
    }

    public async Task<AccountSettings?> UpdateAccountSettingsAsync(Guid id, AccountSettings settings)
    {
        try
        {
            settings.UpdatedAt = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Patch, $"{_url}/rest/v1/account_settings?id=eq.{id}")
            {
                Content = content
            };
            request.Headers.Add("Prefer", "return=representation");

            var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var list = JsonSerializer.Deserialize<List<AccountSettings>>(result, JsonOptions);
            return list?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update account settings");
            return null;
        }
    }

    public async Task<bool> DeleteAccountSettingsAsync(Guid id)
    {
        try
        {
            var response = await _http.DeleteAsync($"{_url}/rest/v1/account_settings?id=eq.{id}").ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete account settings {Id}", id);
            return false;
        }
    }

    // ── Bridge Settings (Centroid Dropcopy FIX) ──
    // Singleton row. GetBridgeSettingsAsync always returns a row (creates default on first call).

    public async Task<BridgeSettings?> GetBridgeSettingsAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_url}/rest/v1/bridge_settings?select=*&limit=1").ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var list = JsonSerializer.Deserialize<List<BridgeSettings>>(json, JsonOptions) ?? [];
            return list.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch bridge_settings");
            return null;
        }
    }

    public async Task<BridgeSettings?> UpsertBridgeSettingsAsync(BridgeSettings settings)
    {
        try
        {
            settings.UpdatedAt = DateTime.UtcNow;

            // There's a singleton index enforcing at most one row. Prefer UPDATE over INSERT
            // so we never accidentally try to create a second row.
            var existing = await GetBridgeSettingsAsync().ConfigureAwait(false);
            if (existing?.Id is Guid id)
            {
                settings.Id = id;
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var req = new HttpRequestMessage(HttpMethod.Patch, $"{_url}/rest/v1/bridge_settings?id=eq.{id}") { Content = content };
                req.Headers.Add("Prefer", "return=representation");
                var resp = await _http.SendAsync(req).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                var list = JsonSerializer.Deserialize<List<BridgeSettings>>(body, JsonOptions);
                return list?.FirstOrDefault();
            }
            else
            {
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var req = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/bridge_settings") { Content = content };
                req.Headers.Add("Prefer", "return=representation");
                var resp = await _http.SendAsync(req).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                var list = JsonSerializer.Deserialize<List<BridgeSettings>>(body, JsonOptions);
                return list?.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert bridge_settings");
            return null;
        }
    }

    // ── Trading Accounts ──

    public async Task<List<TradingAccount>> GetTradingAccountsAsync(string? source = null)
    {
        try
        {
            var filter = source != null ? $"&source=eq.{source}" : "";
            var response = await _http.GetAsync($"{_url}/rest/v1/trading_accounts?select=*{filter}&order=login").ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<TradingAccount>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch trading accounts");
            return [];
        }
    }

    public async Task<int> UpsertTradingAccountsAsync(IEnumerable<TradingAccount> accounts)
    {
        try
        {
            var list = accounts.ToList();
            if (list.Count == 0) return 0;

            // Batch in chunks of 500
            var total = 0;
            foreach (var chunk in list.Chunk(500))
            {
                var json = JsonSerializer.Serialize(chunk, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/trading_accounts?on_conflict=source,login")
                {
                    Content = content
                };
                request.Headers.Add("Prefer", "resolution=merge-duplicates");

                var response = await _http.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                total += chunk.Length;
            }

            _logger.LogInformation("Upserted {Count} trading accounts to Supabase", total);
            return total;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert trading accounts");
            return 0;
        }
    }

    // ── Moved Accounts ──

    private HashSet<long> _movedLogins = new();
    private DateTime _movedLoginsLastRefresh = DateTime.MinValue;

    public async Task<HashSet<long>> GetMovedLoginsAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _movedLogins.Count > 0 && (DateTime.UtcNow - _movedLoginsLastRefresh).TotalMinutes < 5)
            return _movedLogins;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_url}/rest/v1/moved_accounts?select=login");
            var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var rows = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json, JsonOptions) ?? [];
            _movedLogins = new HashSet<long>(rows.Select(r => r["login"].GetInt64()));
            _movedLoginsLastRefresh = DateTime.UtcNow;
            _logger.LogInformation("Loaded {Count} moved account logins", _movedLogins.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load moved accounts");
        }
        return _movedLogins;
    }

    public async Task<List<Dictionary<string, object>>> GetMovedAccountsAsync()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_url}/rest/v1/moved_accounts?select=*&order=moved_at.desc");
            var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch moved accounts");
            return [];
        }
    }

    // ── Deals ──

    public async Task<List<DealRecord>> GetDealsAsync(string source, DateTime from, DateTime to)
    {
        try
        {
            var fromStr = from.ToString("yyyy-MM-ddTHH:mm:ss");
            var toStr = to.ToString("yyyy-MM-ddTHH:mm:ss");
            var allDeals = new List<DealRecord>();
            var pageSize = 1000; // Supabase default max-rows is 1000
            var offset = 0;

            while (true)
            {
                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"{_url}/rest/v1/deals?source=eq.{source}&deal_time=gte.{fromStr}&deal_time=lte.{toStr}&select=*&order=deal_time&limit={pageSize}&offset={offset}");
                request.Headers.Add("Prefer", "count=exact");

                var response = await _http.SendAsync(request).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var page = JsonSerializer.Deserialize<List<DealRecord>>(json, JsonOptions) ?? [];

                allDeals.AddRange(page);

                if (page.Count < pageSize) break; // Last page
                offset += pageSize;
            }

            _logger.LogInformation("Fetched {Count} deals from Supabase for {Source} ({From} to {To})",
                allDeals.Count, source, fromStr, toStr);
            return allDeals;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch deals from Supabase");
            return [];
        }
    }

    public async Task<DateTime?> GetLastDealTimeAsync(string source)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{_url}/rest/v1/deals?source=eq.{source}&select=deal_time&order=deal_time.desc&limit=1");

            var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var rows = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json, JsonOptions) ?? [];

            if (rows.Count > 0 && rows[0].TryGetValue("deal_time", out var dt))
            {
                var lastTime = dt.GetDateTime();
                _logger.LogInformation("Last deal time in Supabase for {Source}: {Time}", source, lastTime);
                return lastTime;
            }

            _logger.LogInformation("No deals found in Supabase for {Source}", source);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get last deal time from Supabase");
            return null;
        }
    }

    /// <summary>
    /// Delete deal rows by (source, deal_id). Used by the reconciliation sweep to remove
    /// "ghost" deals that exist in Supabase but were deleted from MT5 Manager (dealer
    /// reversals, compliance removals, etc). Returns rows attempted — caller should
    /// compare counts if precise tracking is required.
    /// </summary>
    public async Task<int> DeleteDealsAsync(string source, IEnumerable<long> dealIds)
    {
        var ids = dealIds.Distinct().ToList();
        if (ids.Count == 0) return 0;
        var deleted = 0;
        try
        {
            foreach (var chunk in ids.Chunk(500))
            {
                var idList = string.Join(",", chunk);
                var url = $"{_url}/rest/v1/deals?source=eq.{source}&deal_id=in.({idList})";
                var req = new HttpRequestMessage(HttpMethod.Delete, url);
                // count=exact tells PostgREST to return Content-Range: 0-N-1/N with actual affected rows.
                req.Headers.Add("Prefer", "return=minimal,count=exact");
                var res = await _http.SendAsync(req).ConfigureAwait(false);
                if (!res.IsSuccessStatusCode)
                {
                    var err = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _logger.LogWarning("DeleteDealsAsync batch failed {Status}: {Body}", res.StatusCode, err);
                    continue;
                }
                // Parse "Content-Range: 0-N-1/N" → N is actual deleted count
                var range = res.Content.Headers.ContentRange?.ToString() ?? string.Empty;
                var actualCount = chunk.Length;
                if (!string.IsNullOrEmpty(range) && range.Contains('/'))
                {
                    var tail = range.Substring(range.LastIndexOf('/') + 1);
                    if (int.TryParse(tail, out var n)) actualCount = n;
                }
                deleted += actualCount;
                if (actualCount != chunk.Length)
                    _logger.LogWarning("DeleteDealsAsync: requested {Req} ids but only {Act} rows matched (source={Src})", chunk.Length, actualCount, source);
            }
            _logger.LogInformation("Deleted {Count} ghost deals (source={Source})", deleted, source);
            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteDealsAsync failed");
            return deleted;
        }
    }

    public async Task<int> UpsertDealsAsync(IEnumerable<DealRecord> deals)
    {
        try
        {
            var list = deals.ToList();
            if (list.Count == 0) return 0;

            var total = 0;
            foreach (var chunk in list.Chunk(500))
            {
                var json = JsonSerializer.Serialize(chunk, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/deals?on_conflict=source,deal_id")
                {
                    Content = content
                };
                request.Headers.Add("Prefer", "resolution=merge-duplicates");

                var response = await _http.SendAsync(request).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _logger.LogWarning("Deal upsert batch failed: {Error}", err);
                    continue;
                }
                total += chunk.Length;
            }

            _logger.LogInformation("Upserted {Count} deals to Supabase", total);
            return total;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert deals");
            return 0;
        }
    }

    /// <summary>
    /// Compare incoming deals with stored deals, detect changes, and log audit entries.
    /// </summary>
    public async Task<int> DetectAndLogDealChangesAsync(IEnumerable<DealRecord> incomingDeals, string source)
    {
        try
        {
            var incoming = incomingDeals.ToList();
            if (incoming.Count == 0) return 0;

            // Get existing deals for comparison
            var dealIds = incoming.Select(d => d.DealId).Distinct().ToList();
            var existing = new Dictionary<long, DealRecord>();

            foreach (var chunk in dealIds.Chunk(100))
            {
                var ids = string.Join(",", chunk.Select(id => $"\"{id}\""));
                var response = await _http.GetAsync(
                    $"{_url}/rest/v1/deals?source=eq.{source}&deal_id=in.({string.Join(",", chunk)})&select=*");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var records = JsonSerializer.Deserialize<List<DealRecord>>(json, JsonOptions) ?? [];
                    foreach (var r in records)
                        existing[r.DealId] = r;
                }
            }

            // Compare and detect changes
            var auditEntries = new List<TradeAuditEntry>();
            foreach (var deal in incoming)
            {
                if (!existing.TryGetValue(deal.DealId, out var old)) continue;

                void Check(string field, string? oldVal, string? newVal)
                {
                    if (oldVal != newVal)
                    {
                        auditEntries.Add(new TradeAuditEntry
                        {
                            Source = source,
                            DealId = deal.DealId,
                            PositionId = deal.PositionId,
                            Login = deal.Login,
                            Symbol = deal.Symbol,
                            FieldChanged = field,
                            OldValue = oldVal,
                            NewValue = newVal,
                            ChangeType = "modified"
                        });
                    }
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
                await InsertAuditEntriesAsync(auditEntries).ConfigureAwait(false);
                _logger.LogWarning("Detected {Count} deal modifications for source={Source}", auditEntries.Count, source);
            }

            return auditEntries.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect deal changes");
            return 0;
        }
    }

    // ── Audit Log ──

    public async Task InsertAuditEntriesAsync(IEnumerable<TradeAuditEntry> entries)
    {
        try
        {
            var list = entries.ToList();
            if (list.Count == 0) return;

            foreach (var chunk in list.Chunk(500))
            {
                var json = JsonSerializer.Serialize(chunk, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/trade_audit_log")
                {
                    Content = content
                };

                var response = await _http.SendAsync(request).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _logger.LogWarning("Audit log insert failed: {Error}", err);
                }
            }

            _logger.LogInformation("Inserted {Count} audit log entries", list.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert audit entries");
        }
    }

    // ── Alert Rules ──

    public async Task<List<RiskThreshold>> GetAlertRulesAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_url}/rest/v1/alert_rules?select=*&order=created_at").ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<RiskThreshold>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch alert rules from Supabase");
            return [];
        }
    }

    public async Task<RiskThreshold?> UpsertAlertRuleAsync(RiskThreshold rule)
    {
        try
        {
            rule.UpdatedAt = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(rule, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/alert_rules")
            {
                Content = content
            };
            request.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");

            var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var list = JsonSerializer.Deserialize<List<RiskThreshold>>(result, JsonOptions);
            return list?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert alert rule");
            return null;
        }
    }

    public async Task<bool> DeleteAlertRuleAsync(Guid id)
    {
        try
        {
            var response = await _http.DeleteAsync($"{_url}/rest/v1/alert_rules?id=eq.{id}").ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete alert rule {Id}", id);
            return false;
        }
    }

    // ── Alert Events ──

    public async Task<int> InsertAlertEventsAsync(IEnumerable<AlertEvent> events)
    {
        try
        {
            var list = events.ToList();
            if (list.Count == 0) return 0;

            var json = JsonSerializer.Serialize(list, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/alert_events")
            {
                Content = content
            };

            var response = await _http.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogWarning("Alert events insert failed: {Error}", err);
                return 0;
            }
            return list.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert alert events");
            return 0;
        }
    }

    public async Task<List<AlertEvent>> GetAlertEventsAsync(bool unacknowledgedOnly = false, int limit = 100)
    {
        try
        {
            var filter = unacknowledgedOnly ? "&acknowledged=eq.false" : "";
            var response = await _http.GetAsync(
                $"{_url}/rest/v1/alert_events?select=*{filter}&order=triggered_at.desc&limit={limit}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<AlertEvent>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch alert events");
            return [];
        }
    }

    public async Task<bool> AcknowledgeAlertEventAsync(Guid id)
    {
        try
        {
            var body = JsonSerializer.Serialize(new { acknowledged = true, acknowledged_at = DateTime.UtcNow }, JsonOptions);
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Patch, $"{_url}/rest/v1/alert_events?id=eq.{id}")
            {
                Content = content
            };

            var response = await _http.SendAsync(request).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acknowledge alert event {Id}", id);
            return false;
        }
    }

    // ── Audit Log ──

    public async Task<List<TradeAuditEntry>> GetAuditLogAsync(DateTime? from = null, string? symbol = null, long? login = null)
    {
        try
        {
            var filters = new List<string>();
            if (from != null) filters.Add($"detected_at=gte.{from:yyyy-MM-ddTHH:mm:ss}");
            if (symbol != null) filters.Add($"symbol=eq.{symbol}");
            if (login != null) filters.Add($"login=eq.{login}");

            var filter = filters.Count > 0 ? "&" + string.Join("&", filters) : "";
            var response = await _http.GetAsync(
                $"{_url}/rest/v1/trade_audit_log?select=*{filter}&order=detected_at.desc&limit=500");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<TradeAuditEntry>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch audit log");
            return [];
        }
    }

    // =========================================================================
    // Exposure snapshots (point-in-time floating P&L for Period P&L feature)
    // =========================================================================

    /// <summary>Bulk-upsert snapshot rows. Uses (canonical_symbol, snapshot_time) unique index.</summary>
    public async Task<int> UpsertExposureSnapshotsAsync(IEnumerable<ExposureSnapshot> snapshots)
    {
        var batch = snapshots.ToList();
        if (batch.Count == 0) return 0;
        try
        {
            var body = JsonSerializer.Serialize(batch, JsonOptions);
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var req = new HttpRequestMessage(HttpMethod.Post,
                $"{_url}/rest/v1/exposure_snapshots?on_conflict=canonical_symbol,snapshot_time")
            { Content = content };
            req.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
            var res = await _http.SendAsync(req).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                var errBody = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogWarning("exposure_snapshots upsert failed: {Status} {Body}", res.StatusCode, errBody);
                return 0;
            }
            return batch.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpsertExposureSnapshotsAsync failed");
            return 0;
        }
    }

    /// <summary>
    /// For each canonical symbol, fetch the latest snapshot with snapshot_time &lt;= anchor.
    /// Returns a dictionary keyed by canonical_symbol (upper).
    /// </summary>
    public async Task<Dictionary<string, ExposureSnapshot>> GetNearestSnapshotsBeforeAsync(DateTime anchorUtc)
    {
        try
        {
            // Server-side DISTINCT ON via RPC — returns only the latest row per symbol
            // (≤ 30 rows vs the 3.8K+ rows we were pulling before).
            var url = $"{_url}/rest/v1/rpc/latest_snapshots_before";
            var payload = JsonSerializer.Serialize(new { anchor = anchorUtc.ToString("o") }, JsonOptions);
            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            var res = await _http.SendAsync(req).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("latest_snapshots_before RPC failed {Status}", res.StatusCode);
                return new();
            }
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var rows = JsonSerializer.Deserialize<List<ExposureSnapshot>>(json, JsonOptions) ?? new();
            var result = new Dictionary<string, ExposureSnapshot>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in rows)
            {
                var key = r.CanonicalSymbol.ToUpperInvariant();
                result[key] = r;
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetNearestSnapshotsBeforeAsync failed");
            return new();
        }
    }

    /// <summary>List raw snapshots in a window (for history UI / debugging).</summary>
    public async Task<List<ExposureSnapshot>> ListExposureSnapshotsAsync(DateTime fromUtc, DateTime toUtc, string? canonicalSymbol = null)
    {
        try
        {
            var sb = new StringBuilder($"{_url}/rest/v1/exposure_snapshots?select=*");
            sb.Append($"&snapshot_time=gte.{Uri.EscapeDataString(fromUtc.ToString("o"))}");
            sb.Append($"&snapshot_time=lte.{Uri.EscapeDataString(toUtc.ToString("o"))}");
            if (!string.IsNullOrEmpty(canonicalSymbol))
                sb.Append($"&canonical_symbol=eq.{Uri.EscapeDataString(canonicalSymbol)}");
            sb.Append("&order=snapshot_time.desc&limit=2000");
            var res = await _http.GetAsync(sb.ToString()).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode) return new();
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<ExposureSnapshot>>(json, JsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListExposureSnapshotsAsync failed");
            return new();
        }
    }

    // =========================================================================
    // Fast per-symbol B-Book settled P&L via SQL function — avoids pulling 200K+
    // deal rows client-side for long date ranges (turns 46s into <1s).
    // =========================================================================
    public async Task<Dictionary<string, decimal>> AggregateBBookSettledPnlAsync(
        DateTime fromUtc, DateTime toUtc, IEnumerable<long> excludedLogins)
    {
        try
        {
            var body = new
            {
                from_ts = fromUtc.ToUniversalTime().ToString("o"),
                to_ts = toUtc.ToUniversalTime().ToString("o"),
                excluded_logins = excludedLogins.ToArray(),
            };
            var json = JsonSerializer.Serialize(body, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var res = await _http.PostAsync($"{_url}/rest/v1/rpc/aggregate_bbook_settled_pnl", content).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                var txt = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogWarning("aggregate_bbook_settled_pnl RPC failed {Status}: {Body}", res.StatusCode, txt);
                return new(StringComparer.Ordinal);
            }
            var text = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(text);
            var result = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var key = row.GetProperty("canonical_key").GetString() ?? "";
                var pnl = row.GetProperty("net_pnl").GetDecimal();
                if (!string.IsNullOrEmpty(key)) result[key] = pnl;
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AggregateBBookSettledPnlAsync failed");
            return new(StringComparer.Ordinal);
        }
    }

    // Fast per-symbol full aggregation — used by /api/exposure/pnl. Includes volume
    // and direction breakdown on top of net P&L so the P&L tab can render the full
    // grid with a single sub-second RPC instead of paginating 100K+ rows.
    public async Task<List<SymbolPnL>> AggregateBBookPnLFullAsync(
        DateTime fromUtc, DateTime toUtc, IEnumerable<long> excludedLogins)
    {
        try
        {
            var body = new
            {
                from_ts = fromUtc.ToUniversalTime().ToString("o"),
                to_ts = toUtc.ToUniversalTime().ToString("o"),
                excluded_logins = excludedLogins.ToArray(),
            };
            var json = JsonSerializer.Serialize(body, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var res = await _http.PostAsync($"{_url}/rest/v1/rpc/aggregate_bbook_pnl_full", content).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                var txt = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogWarning("aggregate_bbook_pnl_full RPC failed {Status}: {Body}", res.StatusCode, txt);
                return new();
            }
            var text = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(text);
            var result = new List<SymbolPnL>();
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                result.Add(new SymbolPnL
                {
                    Symbol          = row.GetProperty("symbol").GetString() ?? "",
                    DealCount       = (int)row.GetProperty("deal_count").GetInt64(),
                    TotalProfit     = row.GetProperty("total_profit").GetDecimal(),
                    TotalCommission = row.GetProperty("total_commission").GetDecimal(),
                    TotalSwap       = row.GetProperty("total_swap").GetDecimal(),
                    TotalFee        = row.GetProperty("total_fee").GetDecimal(),
                    TotalVolume     = row.GetProperty("total_volume").GetDecimal(),
                    BuyVolume       = row.GetProperty("buy_volume").GetDecimal(),
                    SellVolume      = row.GetProperty("sell_volume").GetDecimal(),
                });
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AggregateBBookPnLFullAsync failed");
            return new();
        }
    }

    // =========================================================================
    // Reconciliation-run audit log
    // =========================================================================

    public async Task<ReconciliationRun?> InsertReconciliationRunAsync(ReconciliationRun run)
    {
        try
        {
            run.Id = null; // let Supabase assign
            var json = JsonSerializer.Serialize(run, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var req = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/reconciliation_runs") { Content = content };
            req.Headers.Add("Prefer", "return=representation");
            var res = await _http.SendAsync(req).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                var errBody = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogWarning("InsertReconciliationRunAsync failed {Status}: {Body}", res.StatusCode, errBody);
                return null;
            }
            var body = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var list = JsonSerializer.Deserialize<List<ReconciliationRun>>(body, JsonOptions);
            return list?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InsertReconciliationRunAsync failed");
            return null;
        }
    }

    public async Task<List<ReconciliationRun>> ListReconciliationRunsAsync(int limit = 50)
    {
        try
        {
            var res = await _http.GetAsync($"{_url}/rest/v1/reconciliation_runs?select=*&order=started_at.desc&limit={Math.Clamp(limit, 1, 500)}").ConfigureAwait(false);
            if (!res.IsSuccessStatusCode) return new();
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<ReconciliationRun>>(json, JsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListReconciliationRunsAsync failed");
            return new();
        }
    }

    // =========================================================================
    // Snapshot schedules CRUD
    // =========================================================================

    public async Task<List<SnapshotSchedule>> GetSnapshotSchedulesAsync()
    {
        try
        {
            var res = await _http.GetAsync($"{_url}/rest/v1/snapshot_schedules?select=*&order=name.asc").ConfigureAwait(false);
            if (!res.IsSuccessStatusCode) return new();
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<SnapshotSchedule>>(json, JsonOptions) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSnapshotSchedulesAsync failed");
            return new();
        }
    }

    public async Task<SnapshotSchedule?> UpsertSnapshotScheduleAsync(SnapshotSchedule schedule)
    {
        try
        {
            var hasId = schedule.Id.HasValue && schedule.Id.Value != Guid.Empty;
            HttpRequestMessage req;
            if (hasId)
            {
                var body = JsonSerializer.Serialize(schedule, JsonOptions);
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                req = new HttpRequestMessage(HttpMethod.Patch,
                    $"{_url}/rest/v1/snapshot_schedules?id=eq.{schedule.Id}")
                { Content = content };
            }
            else
            {
                var body = JsonSerializer.Serialize(schedule, JsonOptions);
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                req = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/snapshot_schedules") { Content = content };
            }
            req.Headers.Add("Prefer", "return=representation");
            var res = await _http.SendAsync(req).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                var errBody = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogWarning("snapshot_schedules upsert failed {Status}: {Body}", res.StatusCode, errBody);
                return null;
            }
            var json = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var list = JsonSerializer.Deserialize<List<SnapshotSchedule>>(json, JsonOptions);
            return list?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpsertSnapshotScheduleAsync failed");
            return null;
        }
    }

    public async Task<bool> DeleteSnapshotScheduleAsync(Guid id)
    {
        try
        {
            var res = await _http.DeleteAsync($"{_url}/rest/v1/snapshot_schedules?id=eq.{id}").ConfigureAwait(false);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteSnapshotScheduleAsync failed");
            return false;
        }
    }
}
