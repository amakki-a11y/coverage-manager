using System.Net;
using System.Text;
using CoverageManager.Api.Services;
using CoverageManager.Core.Engines;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Outcome = CoverageManager.Api.Services.CollectorPositionsPoller.PollOutcome;

namespace CoverageManager.Tests;

/// <summary>
/// H2: v2 reads coverage positions from the collector's GET /positions. The collector answers [] both for a
/// flat book and for a failed MT5 call, so an empty list must never wipe coverage unless /health confirms it twice.
/// </summary>
[TestClass]
public class CollectorPositionsPollerTests
{
    private const string OnePosition =
        """[{"symbol":"GOLD","direction":"BUY","volume":500,"openPrice":2400.5,"currentPrice":2410.25,"profit":4875,"swap":-1.5,"ticket":9001}]""";
    private const string HealthOk =
        """{"status":"ok","terminal":"x","login":96900,"server":"s","last_position_update_utc":"2026-09-13T00:00:00Z","stale":false}""";
    private const string HealthStale =
        """{"status":"stale","terminal":"x","login":96900,"server":"s","last_position_update_utc":null,"stale":true}""";

    private sealed class Handler : HttpMessageHandler
    {
        public Func<string, HttpResponseMessage> Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(Respond(request.RequestUri!.AbsolutePath));
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private Handler _handler = null!;
    private PositionManager _pm = null!;
    private int _dirty;
    private CollectorPositionsPoller _poller = null!;
    private string _positions = OnePosition;
    private string? _health = HealthOk;

    [TestInitialize]
    public void Setup()
    {
        _handler = new Handler();
        _handler.Respond = path => path switch
        {
            "/positions" => Json(_positions),
            "/health" => _health is null ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Json(_health),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        };
        _pm = new PositionManager();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Coverage:PollEnabled"] = "true",
            ["Coverage:CollectorUrl"] = "http://collector.test",
        }).Build();
        _poller = new CollectorPositionsPoller(new Factory(_handler), _pm, () => _dirty++, config,
            NullLogger<CollectorPositionsPoller>.Instance);
    }

    [TestMethod]
    public async Task NonEmptyList_IsApplied_WithLoginFromHealth_AndMarksDirty()
    {
        Assert.AreEqual(Outcome.Applied, await _poller.PollOnceAsync());

        var p = _pm.GetCoveragePositions().Single();
        Assert.AreEqual("GOLD", p.Symbol);
        Assert.AreEqual("BUY", p.Direction);
        Assert.AreEqual(500m, p.VolumeLots);
        Assert.AreEqual(4875m, p.Profit);
        Assert.AreEqual(96900UL, p.Login);
        Assert.AreEqual(1, _dirty);
        Assert.AreEqual(1, _poller.Status.LastAppliedCount);
        Assert.IsFalse(_poller.Status.Stale);
    }

    [TestMethod]
    public async Task EmptyList_WithHealthyCollector_IsHeldOnce_ThenApplied()
    {
        await _poller.PollOnceAsync();
        _positions = "[]";

        Assert.AreEqual(Outcome.EmptyPendingConfirmation, await _poller.PollOnceAsync());
        Assert.AreEqual(1, _pm.GetCoveragePositions().Count(), "one empty answer must not wipe coverage");

        Assert.AreEqual(Outcome.AppliedEmpty, await _poller.PollOnceAsync());
        Assert.AreEqual(0, _pm.GetCoveragePositions().Count());
        Assert.AreEqual(1L, _poller.Status.AmbiguousEmptySkipped);
    }

    [TestMethod]
    public async Task EmptyList_WhileCollectorUnhealthyOrUnreadable_IsNeverApplied()
    {
        await _poller.PollOnceAsync();
        _positions = "[]";

        _health = HealthStale;
        for (var i = 0; i < 5; i++)
            Assert.AreEqual(Outcome.EmptySkippedUnhealthy, await _poller.PollOnceAsync());

        _health = null;   // /health 503
        for (var i = 0; i < 3; i++)
            Assert.AreEqual(Outcome.EmptySkippedUnhealthy, await _poller.PollOnceAsync());

        Assert.AreEqual(1, _pm.GetCoveragePositions().Count());
        Assert.AreEqual(1, _dirty);
    }

    [TestMethod]
    public async Task EmptyConfirmation_IsResetByAnInterveningUnhealthyPoll()
    {
        await _poller.PollOnceAsync();
        _positions = "[]";
        Assert.AreEqual(Outcome.EmptyPendingConfirmation, await _poller.PollOnceAsync());
        _health = HealthStale;
        Assert.AreEqual(Outcome.EmptySkippedUnhealthy, await _poller.PollOnceAsync());
        _health = HealthOk;
        Assert.AreEqual(Outcome.EmptyPendingConfirmation, await _poller.PollOnceAsync(),
            "the two confirmed empties must be consecutive");
        Assert.AreEqual(1, _pm.GetCoveragePositions().Count());
    }

    [TestMethod]
    public async Task HttpErrorOrGarbage_KeepsLastSnapshot_AndCountsFailures()
    {
        await _poller.PollOnceAsync();

        _handler.Respond = path => path == "/positions"
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : Json(HealthOk);
        Assert.AreEqual(Outcome.Failed, await _poller.PollOnceAsync());

        _handler.Respond = path => path == "/positions" ? Json("not json") : Json(HealthOk);
        Assert.AreEqual(Outcome.Failed, await _poller.PollOnceAsync());

        _handler.Respond = _ => throw new HttpRequestException("connection refused");
        Assert.AreEqual(Outcome.Failed, await _poller.PollOnceAsync());

        Assert.AreEqual(1, _pm.GetCoveragePositions().Count());
        var s = _poller.Status;
        Assert.AreEqual(3L, s.Failures);
        Assert.AreEqual(3, s.ConsecutiveFailures);
        Assert.IsNotNull(s.LastError);
    }

    [TestMethod]
    public async Task NewSnapshot_ReplacesThePreviousOne()
    {
        await _poller.PollOnceAsync();
        _positions = """[{"symbol":"GOLD","direction":"SELL","volume":200,"openPrice":1,"currentPrice":1,"profit":0,"swap":0,"ticket":9002}]""";
        await _poller.PollOnceAsync();

        var p = _pm.GetCoveragePositions().Single();
        Assert.AreEqual("SELL", p.Direction);
        Assert.AreEqual(200m, p.VolumeLots);
    }

    [TestMethod]
    public void Disabled_ByDefault_WhenTheKeyIsAbsent()
    {
        var poller = new CollectorPositionsPoller(new Factory(_handler), _pm, () => { },
            new ConfigurationBuilder().Build(), NullLogger<CollectorPositionsPoller>.Instance);
        Assert.IsFalse(poller.Status.Enabled);
        Assert.IsFalse(poller.Status.Stale);
    }
}
