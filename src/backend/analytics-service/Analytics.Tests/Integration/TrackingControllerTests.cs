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
    /// <summary>The gateway-validated organization every ping in this fixture claims to come from.</summary>
    private static readonly Guid OrganizationId = Guid.Parse("33333333-3333-3333-3333-333333333333");

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

    /// <summary>
    /// An empty body must never reach <c>TryRecord(null)</c>: the endpoint answers 400, not 500.
    /// </summary>
    [Test]
    public async Task TrackEvent_NullBody_Returns400()
    {
        var client = _factory.CreateAuthenticatedClient();

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

    [Test]
    public async Task Ping_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/tracking/presence/ping", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Simulates the API gateway injecting <c>X-User-Id</c>; the presence tracker is stubbed.
    /// </summary>
    [Test]
    public async Task Ping_WithXUserIdHeader_Returns204()
    {
        var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("X-User-Id", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-Organization-Id", OrganizationId.ToString());

        var response = await client.PostAsync("/tracking/presence/ping", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.PresenceTracker.Received(1).MarkSeenAsync(
            OrganizationId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// No <c>X-User-Id</c> header — identity must be resolved from the validated JWT subject.
    /// </summary>
    [Test]
    public async Task Ping_AuthenticatedViaJwt_Returns204()
    {
        var userId = Guid.NewGuid();
        var client = _factory.CreateAuthenticatedClient(userId);
        client.DefaultRequestHeaders.Add("X-Organization-Id", OrganizationId.ToString());

        var response = await client.PostAsync("/tracking/presence/ping", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.PresenceTracker.Received(1).MarkSeenAsync(
            OrganizationId, userId.ToString(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Phase 40.13. Presence is stored per organization, so a ping with no organization header has
    /// no correct destination. The route is [TenantScoped], so the tenant middleware rejects it
    /// before the action runs — it must never fall back to a shared key.
    /// </summary>
    [Test]
    public async Task Ping_WithoutAnOrganizationHeader_IsRejectedAndRecordsNothing()
    {
        var client = _factory.CreateAuthenticatedClient(Guid.NewGuid());

        var response = await client.PostAsync("/tracking/presence/ping", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _factory.PresenceTracker.DidNotReceive().MarkSeenAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
