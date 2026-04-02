using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CoverageManager.Core.Models;

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
            var response = await _http.GetAsync($"{_url}/rest/v1/symbol_mappings?is_active=eq.true&select=*");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
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

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();
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
            var response = await _http.DeleteAsync($"{_url}/rest/v1/symbol_mappings?id=eq.{id}");
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
            var response = await _http.GetAsync($"{_url}/rest/v1/account_settings?select=*&order=account_type,created_at");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
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

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();
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

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync();
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
            var response = await _http.DeleteAsync($"{_url}/rest/v1/account_settings?id=eq.{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete account settings {Id}", id);
            return false;
        }
    }

    // ── Trading Accounts ──

    public async Task<List<TradingAccount>> GetTradingAccountsAsync(string? source = null)
    {
        try
        {
            var filter = source != null ? $"&source=eq.{source}" : "";
            var response = await _http.GetAsync($"{_url}/rest/v1/trading_accounts?select=*{filter}&order=login");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
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
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/trading_accounts")
                {
                    Content = content
                };
                request.Headers.Add("Prefer", "resolution=merge-duplicates");

                var response = await _http.SendAsync(request);
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

    // ── Deals ──

    public async Task<List<DealRecord>> GetDealsAsync(string source, DateTime from, DateTime to)
    {
        try
        {
            var fromStr = from.ToString("yyyy-MM-ddTHH:mm:ss");
            var toStr = to.ToString("yyyy-MM-ddTHH:mm:ss");
            var response = await _http.GetAsync(
                $"{_url}/rest/v1/deals?source=eq.{source}&deal_time=gte.{fromStr}&deal_time=lte.{toStr}&select=*&order=deal_time");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<DealRecord>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch deals from Supabase");
            return [];
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
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/deals")
                {
                    Content = content
                };
                request.Headers.Add("Prefer", "resolution=merge-duplicates");

                var response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
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
                    var json = await response.Content.ReadAsStringAsync();
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
                await InsertAuditEntriesAsync(auditEntries);
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

                var response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
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
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<TradeAuditEntry>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch audit log");
            return [];
        }
    }
}
