using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Sellevate.Gateway.Tests;

/// <summary>
/// Boots the gateway in-process and reads its live reverse-proxy configuration to prove
/// the <c>/notifications/*</c> slice has been flipped off the monolith and onto the
/// dedicated notification cluster (Phase 4.4).
/// </summary>
[TestFixture]
public class NotificationRouteFlipTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private IConfiguration _configuration = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        _factory = new WebApplicationFactory<Program>();
        _configuration = _factory.Services.GetRequiredService<IConfiguration>();
    }

    [OneTimeTearDown]
    public void TearDown() => _factory.Dispose();

    [Test]
    public void Notifications_collection_route_targets_the_notification_cluster()
    {
        _configuration["ReverseProxy:Routes:notification-notifications:ClusterId"].Should().Be("notification");
        _configuration["ReverseProxy:Routes:notification-notifications:Match:Path"]
            .Should().Be("/notifications/{**catch-all}");
    }

    [Test]
    public void Notifications_root_route_targets_the_notification_cluster()
    {
        _configuration["ReverseProxy:Routes:notification-notifications-root:ClusterId"].Should().Be("notification");
        _configuration["ReverseProxy:Routes:notification-notifications-root:Match:Path"].Should().Be("/notifications");
    }

    [Test]
    public void Notification_cluster_has_a_destination()
    {
        _configuration["ReverseProxy:Clusters:notification:Destinations:d1:Address"]
            .Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public void Notifications_are_no_longer_served_by_the_monolith()
    {
        _configuration["ReverseProxy:Routes:notification-notifications:ClusterId"].Should().NotBe("monolith");
    }
}
