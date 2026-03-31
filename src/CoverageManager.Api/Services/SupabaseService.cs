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
}
