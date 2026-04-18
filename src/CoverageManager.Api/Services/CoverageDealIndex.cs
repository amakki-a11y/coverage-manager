using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoverageManager.Api.Services;

/// <summary>
/// Indexes coverage-side MT5 deals from the Python collector by (MT5 order ticket -> MT5 deal ticket).
///
/// Use case: FXGROW books each Centroid LP leg as an MT5 order on the 96900 terminal using
/// Centroid's maker_order_id as the MT5 order ticket. That lets us enrich each Bridge COV fill
/// with the true coverage MT5 deal number for deal-per-deal reconciliation.
///
/// Runs as a hosted service: polls /deals/raw every 10s for today's window, refreshes the index.
/// Index is (orderId -> CoverageDeal) so CovFill.MakerOrderId -> CovFill.MtDealId is O(1).
/// </summary>
public class CoverageDealIndex : BackgroundService
{
    private readonly HttpClient _http;
    private readonly ILogger<CoverageDealIndex> _logger;
    private readonly SupabaseService _supabase;
    private readonly ConcurrentDictionary<ulong, CoverageDeal> _byOrder = new();
    // Keyed by Centroid maker_order_id (which FXGROW writes into MT5 external_id on 96900).
    // One maker_order_id can produce multiple MT5 deals (partial fills); we keep the earliest.
    private readonly ConcurrentDictionary<ulong, CoverageDeal> _byExternalId = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public CoverageDealIndex(HttpClient http, SupabaseService supabase, ILogger<CoverageDealIndex> logger)
    {
        _http = http;
        _http.BaseAddress = new Uri("http://localhost:8100/");
        _http.Timeout = TimeSpan.FromSeconds(30);
        _supabase = supabase;
        _logger = logger;
    }

    /// <summary>Returns the coverage MT5 deal ticket for a given MT5 order ticket, or null.</summary>
    public ulong? GetDealIdByOrder(ulong orderId)
    {
        if (orderId == 0) return null;
        return _byOrder.TryGetValue(orderId, out var d) ? d.Ticket : null;
    }

    /// <summary>
    /// Returns the coverage MT5 deal ticket for a Centroid maker_order_id
    /// (FXGROW writes it into MT5.external_id on the 96900 account).
    /// </summary>
    public ulong? GetDealIdByMakerOrderId(ulong makerOrderId)
    {
        if (makerOrderId == 0) return null;
        return _byExternalId.TryGetValue(makerOrderId, out var d) ? d.Ticket : null;
    }

    /// <summary>Returns the full coverage deal (ticket + symbol + price etc.) for a given order ticket.</summary>
    public CoverageDeal? GetByOrder(ulong orderId)
    {
        if (orderId == 0) return null;
        return _byOrder.TryGetValue(orderId, out var d) ? d : null;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Gate on bridge_settings.enabled — when the Bridge feed is disabled, there's no
        // point polling the collector for maker-order ↔ coverage-deal mapping.
        try
        {
            var s = await _supabase.GetBridgeSettingsAsync();
            if (s?.Enabled == false)
            {
                _logger.LogInformation("CoverageDealIndex disabled (bridge_settings.enabled=false) — poller will not start");
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CoverageDealIndex: failed to read bridge_settings — starting anyway");
        }

        _logger.LogInformation("CoverageDealIndex starting");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CoverageDealIndex refresh failed (will retry)");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
        _logger.LogInformation("CoverageDealIndex stopped");
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        var url = $"deals/raw?from={yesterday}&to={today}";

        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("collector /deals/raw returned {Status}", (int)resp.StatusCode);
            return;
        }

        var body = await resp.Content.ReadAsStreamAsync(ct);
        var payload = await JsonSerializer.DeserializeAsync<Payload>(body, JsonOpts, ct);
        if (payload?.Deals == null) return;

        int added = 0, addedExt = 0;
        foreach (var d in payload.Deals)
        {
            if (d.Ticket == 0) continue;
            if (d.Order != 0)
            {
                if (_byOrder.TryAdd(d.Order, d)) added++;
                else _byOrder[d.Order] = d;
            }
            if (!string.IsNullOrEmpty(d.ExternalId) && ulong.TryParse(d.ExternalId, out var extId) && extId != 0)
            {
                // Keep the earliest deal when multiple (partial fills) share the same external_id.
                if (_byExternalId.TryAdd(extId, d)) addedExt++;
            }
        }
        if (added > 0 || addedExt > 0)
            _logger.LogDebug(
                "CoverageDealIndex: +{Added} order / +{AddedExt} external_id mappings (totals: order={Total}, ext={ExtTotal})",
                added, addedExt, _byOrder.Count, _byExternalId.Count);
    }

    private sealed class Payload
    {
        [JsonPropertyName("deals")] public List<CoverageDeal>? Deals { get; set; }
    }
}

public sealed class CoverageDeal
{
    [JsonPropertyName("ticket")]     public ulong Ticket { get; set; }
    [JsonPropertyName("order")]      public ulong Order { get; set; }
    [JsonPropertyName("externalId")] public string? ExternalId { get; set; }
    [JsonPropertyName("symbol")]     public string? Symbol { get; set; }
    [JsonPropertyName("type")]       public string? Type { get; set; }
    [JsonPropertyName("volume")]     public decimal Volume { get; set; }
    [JsonPropertyName("price")]      public decimal Price { get; set; }
    [JsonPropertyName("entry")]      public int Entry { get; set; }
    [JsonPropertyName("time")]       public string? Time { get; set; }
    [JsonPropertyName("positionId")] public ulong PositionId { get; set; }
}
