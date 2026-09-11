using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace CoverageManager.Api.Services;

/// <summary>
/// The writes the read-only guard refused, per method and path, for <c>/api/exposure/diagnostics.supabaseReadOnly</c>.
/// Registered always; <see cref="Enabled"/> says whether the guard is active.
/// </summary>
public sealed class SupabaseReadOnlyLedger
{
    private readonly ConcurrentDictionary<string, long> _blocked = new(StringComparer.Ordinal);

    public SupabaseReadOnlyLedger(bool enabled, string host)
    {
        Enabled = enabled;
        Host = host;
    }

    public bool Enabled { get; }
    public string Host { get; }
    public long Total => _blocked.Values.Sum();
    public IReadOnlyDictionary<string, long> Blocked => new Dictionary<string, long>(_blocked, StringComparer.Ordinal);

    internal long Count(string key) => _blocked.AddOrUpdate(key, 1, (_, c) => c + 1);
}

/// <summary>
/// <c>Supabase:ReadOnly = true</c> (env <c>Supabase__ReadOnly=true</c>): every write to the Supabase host - POST, PUT,
/// PATCH, DELETE outside <c>/rest/v1/rpc/</c> - is answered locally with <c>200 []</c> and never sent; GETs and the
/// read-only RPC functions pass through. Installed on every client of the <see cref="IHttpClientFactory"/>, so it covers
/// <see cref="SupabaseService"/>, <see cref="BridgeSupabaseWriter"/> and the ad-hoc clients alike. Meant for running the
/// API against a live feed without persisting anything, e.g. a feed test from a parked install.
/// </summary>
public sealed class SupabaseReadOnlyHandler : DelegatingHandler
{
    private readonly SupabaseReadOnlyLedger _ledger;
    private readonly ILogger<SupabaseReadOnlyHandler> _logger;

    public SupabaseReadOnlyHandler(SupabaseReadOnlyLedger ledger, ILogger<SupabaseReadOnlyHandler> logger)
    {
        _ledger = ledger;
        _logger = logger;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri;
        if (!_ledger.Enabled
            || uri is null
            || !string.Equals(uri.Host, _ledger.Host, StringComparison.OrdinalIgnoreCase)
            || request.Method == HttpMethod.Get
            || request.Method == HttpMethod.Head
            || request.Method == HttpMethod.Options
            || uri.AbsolutePath.StartsWith("/rest/v1/rpc/", StringComparison.Ordinal))
        {
            return base.SendAsync(request, cancellationToken);
        }

        var key = request.Method.Method + " " + uri.AbsolutePath;
        if (_ledger.Count(key) == 1)
            _logger.LogWarning("Supabase read-only: blocked {Method} {Path} (further blocks of this path are counted, not logged)",
                request.Method.Method, uri.AbsolutePath);

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            ReasonPhrase = "OK (blocked by Supabase:ReadOnly)",
            Content = new StringContent("[]", Encoding.UTF8, "application/json"),
        };
        return Task.FromResult(response);
    }
}
