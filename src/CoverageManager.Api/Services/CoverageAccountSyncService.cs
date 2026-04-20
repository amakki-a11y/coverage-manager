using System.Text.Json;
using CoverageManager.Core.Models;

namespace CoverageManager.Api.Services;

/// <summary>
/// Every 5 min, polls the Python collector's <c>/account</c> endpoint and
/// upserts the LP (coverage) account into <c>trading_accounts</c>. This is
/// the reason the Equity P&amp;L tab now has a Coverage row — the MT5 Manager
/// API our B-Book side uses cannot see accounts on the LP's MT5 server, so
/// the collector (running against the LP's MT5 terminal) is the only source
/// of balance/credit/equity for those logins.
/// </summary>
public class CoverageAccountSyncService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<CoverageAccountSyncService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public CoverageAccountSyncService(
        IServiceProvider services,
        IHttpClientFactory httpFactory,
        ILogger<CoverageAccountSyncService> logger)
    {
        _services = services;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CoverageAccountSyncService starting");
        // First tick after a short delay so startup has time to finish.
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogWarning(ex, "Coverage account sync tick failed"); }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
        _logger.LogInformation("CoverageAccountSyncService stopped");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        CollectorAccount? data;
        try
        {
            // Default collector URL — could be lifted into IConfiguration later.
            var res = await client.GetAsync("http://localhost:8100/account", ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogDebug("collector /account returned {Status} — skipping tick", res.StatusCode);
                return;
            }
            var json = await res.Content.ReadAsStringAsync(ct);
            data = JsonSerializer.Deserialize<CollectorAccount>(json, JsonOptions);
        }
        catch (HttpRequestException)
        {
            // Collector is down — not an error condition for us, just skip.
            return;
        }

        if (data == null || data.Login <= 0) return;

        var acct = new TradingAccount
        {
            Source = "coverage",
            Login = data.Login,
            Name = data.Name ?? "",
            GroupName = "",
            Leverage = data.Leverage,
            Balance = data.Balance,
            Equity = data.Equity,
            Credit = data.Credit,
            Margin = data.Margin,
            FreeMargin = data.FreeMargin,
            Currency = data.Currency ?? "USD",
            Status = "active",
            Comment = data.Server ?? "",
            SyncedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var supabase = _services.GetRequiredService<SupabaseService>();
        var n = await supabase.UpsertTradingAccountsAsync(new[] { acct });
        if (n > 0)
        {
            _logger.LogDebug("Coverage account {Login} synced: bal={Bal} cred={Cred} eq={Eq}",
                acct.Login, acct.Balance, acct.Credit, acct.Equity);
        }
    }

    private sealed class CollectorAccount
    {
        public long Login { get; set; }
        public string? Name { get; set; }
        public string? Server { get; set; }
        public decimal Balance { get; set; }
        public decimal Credit { get; set; }
        public decimal Equity { get; set; }
        public decimal Margin { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("free_margin")]
        public decimal FreeMargin { get; set; }
        public int Leverage { get; set; }
        public string? Currency { get; set; }
    }
}
