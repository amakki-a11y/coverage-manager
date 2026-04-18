using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoverageManager.Core.Models.Bridge;

namespace CoverageManager.Api.Services;

/// <summary>
/// Persists ExecutionPairs to the bridge_executions table via Supabase REST.
/// Separate from SupabaseService (which is domain-specific to deals/accounts/mappings) to keep
/// the Bridge feature's DB concerns isolated.
/// </summary>
public class BridgeSupabaseWriter
{
    private readonly HttpClient _http;
    private readonly string _url;
    private readonly ILogger<BridgeSupabaseWriter> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public BridgeSupabaseWriter(IConfiguration config, HttpClient http, ILogger<BridgeSupabaseWriter> logger)
    {
        _http = http;
        _url = config["Supabase:Url"] ?? throw new ArgumentException("Supabase:Url not configured");
        var key = config["Supabase:Key"] ?? throw new ArgumentException("Supabase:Key not configured");
        _logger = logger;

        _http.DefaultRequestHeaders.Add("apikey", key);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
    }

    public async Task UpsertAsync(ExecutionPair pair, CancellationToken ct = default)
    {
        try
        {
            var row = ToRow(pair);
            var json = JsonSerializer.Serialize(new[] { row }, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var req = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/bridge_executions?on_conflict=client_deal_id") { Content = content };
            req.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");

            var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("bridge_executions upsert failed {Status}: {Body}", resp.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert ExecutionPair {DealId}", pair.ClientDealId);
        }
    }

    public async Task<IReadOnlyList<ExecutionPair>> QueryAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? canonicalSymbol,
        int limit,
        CancellationToken ct = default)
    {
        try
        {
            var sb = new StringBuilder($"{_url}/rest/v1/bridge_executions?select=*");
            sb.Append($"&client_time=gte.{Uri.EscapeDataString(fromUtc.ToString("o"))}");
            sb.Append($"&client_time=lte.{Uri.EscapeDataString(toUtc.ToString("o"))}");
            if (!string.IsNullOrEmpty(canonicalSymbol))
                sb.Append($"&symbol=eq.{Uri.EscapeDataString(canonicalSymbol)}");
            sb.Append("&order=client_time.desc");
            sb.Append($"&limit={Math.Clamp(limit, 1, 5000)}");

            var resp = await _http.GetAsync(sb.ToString(), ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("bridge_executions query failed {Status}: {Body}", resp.StatusCode, body);
                return Array.Empty<ExecutionPair>();
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            var rows = JsonSerializer.Deserialize<List<BridgeRow>>(json, JsonOptions) ?? new();
            return rows.Select(FromRow).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query bridge_executions");
            return Array.Empty<ExecutionPair>();
        }
    }

    // ---------- DTO <-> row mapping ----------

    private static BridgeRow ToRow(ExecutionPair p) => new()
    {
        ClientDealId = p.ClientDealId,
        CenOrdId = p.CenOrdId,
        Symbol = p.Symbol,
        Side = p.Side.ToString(),
        ClientVolume = p.ClientVolume,
        ClientPrice = p.ClientPrice,
        ClientTime = p.ClientTimeUtc,
        ClientMtLogin = p.ClientMtLogin.HasValue ? (long?)p.ClientMtLogin.Value : null,
        ClientMtTicket = p.ClientMtTicket.HasValue ? (long?)p.ClientMtTicket.Value : null,
        ClientMtDealId = p.ClientMtDealId.HasValue ? (long?)p.ClientMtDealId.Value : null,
        CovVolume = p.CovVolume,
        CovFills = p.CovFills,
        AvgCovPrice = p.AvgCovPrice,
        PriceEdge = p.PriceEdge,
        Pips = p.Pips,
        MaxTimeDiffMs = p.MaxTimeDiffMs,
        MinTimeDiffMs = p.MinTimeDiffMs,
    };

    private static ExecutionPair FromRow(BridgeRow r) => new()
    {
        ClientDealId = r.ClientDealId,
        CenOrdId = r.CenOrdId,
        Symbol = r.Symbol,
        Side = Enum.TryParse<BridgeSide>(r.Side, ignoreCase: true, out var side) ? side : BridgeSide.BUY,
        ClientVolume = r.ClientVolume,
        ClientPrice = r.ClientPrice,
        ClientTimeUtc = r.ClientTime,
        ClientMtLogin = r.ClientMtLogin.HasValue ? (ulong?)r.ClientMtLogin.Value : null,
        ClientMtTicket = r.ClientMtTicket.HasValue ? (ulong?)r.ClientMtTicket.Value : null,
        ClientMtDealId = r.ClientMtDealId.HasValue ? (ulong?)r.ClientMtDealId.Value : null,
        CovVolume = r.CovVolume,
        CovFills = r.CovFills ?? new(),
        AvgCovPrice = r.AvgCovPrice ?? 0m,
        PriceEdge = r.PriceEdge ?? 0m,
        Pips = r.Pips ?? 0m,
        CoverageRatio = r.CoverageRatio ?? 0m,
        MaxTimeDiffMs = r.MaxTimeDiffMs ?? 0,
        MinTimeDiffMs = r.MinTimeDiffMs ?? 0,
        CreatedAtUtc = r.CreatedAt,
    };

    private sealed class BridgeRow
    {
        [JsonPropertyName("client_deal_id")] public string ClientDealId { get; set; } = string.Empty;
        [JsonPropertyName("cen_ord_id")] public string CenOrdId { get; set; } = string.Empty;
        [JsonPropertyName("symbol")] public string Symbol { get; set; } = string.Empty;
        [JsonPropertyName("side")] public string Side { get; set; } = "BUY";
        [JsonPropertyName("client_volume")] public decimal ClientVolume { get; set; }
        [JsonPropertyName("client_price")] public decimal ClientPrice { get; set; }
        [JsonPropertyName("client_time")] public DateTime ClientTime { get; set; }
        [JsonPropertyName("client_mt_login")] public long? ClientMtLogin { get; set; }
        [JsonPropertyName("client_mt_ticket")] public long? ClientMtTicket { get; set; }
        [JsonPropertyName("client_mt_deal_id")] public long? ClientMtDealId { get; set; }
        [JsonPropertyName("cov_volume")] public decimal CovVolume { get; set; }
        [JsonPropertyName("cov_fills")] public List<CovFill>? CovFills { get; set; }
        [JsonPropertyName("coverage_ratio")] public decimal? CoverageRatio { get; set; }
        [JsonPropertyName("avg_cov_price")] public decimal? AvgCovPrice { get; set; }
        [JsonPropertyName("price_edge")] public decimal? PriceEdge { get; set; }
        [JsonPropertyName("pips")] public decimal? Pips { get; set; }
        [JsonPropertyName("max_time_diff_ms")] public int? MaxTimeDiffMs { get; set; }
        [JsonPropertyName("min_time_diff_ms")] public int? MinTimeDiffMs { get; set; }
        [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
    }
}
