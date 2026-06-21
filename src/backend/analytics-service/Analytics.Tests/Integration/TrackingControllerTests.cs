using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Analytics.Tests.Helpers;

namespace Sellevate.Analytics.Tests.Integration;

/// <summary>
/// Integration tests for <c>TrackingController</c> using <see cref="AnalyticsWebApplicationFactory"/>.
/// Redis and Kafka are stubbed — no Docker required. Mark [Category("Integration")] so
/// they can be filtered separately in CI if needed.
/// </summary>
[TestFixture]
[Category("Integration")]
public sealed class TrackingControllerTests
{
    private AnalyticsWebApplicationFactory _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new AnalyticsWebApplicationFactory();
    }

    [TearDown]
    public void TearDown()
    {
        _factory.Dispose();
    }

    // ── POST /tracking/events ─────────────────────────────────────────────

    [Test]
    public async Task TrackEvent_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/tracking/events",
            new { @event = "page_view", page = "tree" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task TrackEvent_ValidTokenAndUnknownEvent_Returns400()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync(
            "/tracking/events",
            new { @event = "totally_unknown_event_xyz", page = "tree" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task TrackEvent_NullBody_Returns400()
    {
        var client = _factory.CreateAuthenticatedClient();

        // Send an empty body with no Content-Type — should not reach TryRecord(null)
        // and must return 400 (not 500).
        var response = await client.PostAsync(
            "/tracking/events",
            new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task TrackEvent_PageView_Returns204()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync(
            "/tracking/events",
            new { @event = "page_view", page = "tree" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── POST /tracking/presence/ping ──────────────────────────────────────

    [Test]
    public async Task Ping_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/tracking/presence/ping", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Ping_WithXUserIdHeader_Returns204()
    {
        // Simulate the API gateway injecting X-User-Id (presence tracker is stubbed).
        var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("X-User-Id", Guid.NewGuid().ToString());

        var response = await client.PostAsync("/tracking/presence/ping", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.PresenceTracker.Received(1).MarkSeenAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Ping_AuthenticatedViaJwt_Returns204()
    {
        // No X-User-Id header — identity must be resolved from the validated JWT subject.
        var userId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(userId);

        var response = await client.PostAsync("/tracking/presence/ping", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.PresenceTracker.Received(1).MarkSeenAsync(
            userId.ToString(), Arg.Any<CancellationToken>());
    }
}
