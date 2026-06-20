using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Sellevate.Gateway.Tests;

/// <summary>
/// Boots the gateway in-process and reads its live reverse-proxy configuration to prove
/// the gamification slices (<c>/gamification/*</c>, <c>/league</c>,
/// <c>/profile/achievements</c>, <c>/admin/gamification/*</c>, <c>/admin/leagues/*</c>)
/// have been flipped off the monolith and onto the gamification cluster (Phase 7.4).
/// </summary>
[TestFixture]
public class GamificationRouteFlipTests
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
    public void Gamification_progress_route_targets_the_gamification_cluster()
    {
        _configuration["ReverseProxy:Routes:gamification-progress:ClusterId"].Should().Be("gamification");
        _configuration["ReverseProxy:Routes:gamification-progress:Match:Path"]
            .Should().Be("/gamification/{**catch-all}");
    }

    [Test]
    public void League_route_targets_the_gamification_cluster()
    {
        _configuration["ReverseProxy:Routes:gamification-league:ClusterId"].Should().Be("gamification");
        _configuration["ReverseProxy:Routes:gamification-league:Match:Path"].Should().Be("/league");
    }

    [Test]
    public void Achievements_route_targets_the_gamification_cluster()
    {
        _configuration["ReverseProxy:Routes:gamification-achievements:ClusterId"].Should().Be("gamification");
        _configuration["ReverseProxy:Routes:gamification-achievements:Match:Path"].Should().Be("/profile/achievements");
    }

    [Test]
    public void Admin_gamification_route_targets_the_gamification_cluster()
    {
        _configuration["ReverseProxy:Routes:gamification-admin-gamification:ClusterId"].Should().Be("gamification");
        _configuration["ReverseProxy:Routes:gamification-admin-gamification:Match:Path"]
            .Should().Be("/admin/gamification/{**catch-all}");
    }

    [Test]
    public void Admin_leagues_route_targets_the_gamification_cluster()
    {
        _configuration["ReverseProxy:Routes:gamification-admin-leagues:ClusterId"].Should().Be("gamification");
        _configuration["ReverseProxy:Routes:gamification-admin-leagues:Match:Path"]
            .Should().Be("/admin/leagues/{**catch-all}");
    }

    [Test]
    public void Gamification_cluster_has_a_destination()
    {
        _configuration["ReverseProxy:Clusters:gamification:Destinations:d1:Address"]
            .Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public void Gamification_slices_are_no_longer_served_by_the_monolith()
    {
        _configuration["ReverseProxy:Routes:gamification-progress:ClusterId"].Should().NotBe("monolith");
        _configuration["ReverseProxy:Routes:gamification-league:ClusterId"].Should().NotBe("monolith");
        _configuration["ReverseProxy:Routes:gamification-achievements:ClusterId"].Should().NotBe("monolith");
    }
}
