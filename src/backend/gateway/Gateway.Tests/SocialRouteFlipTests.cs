using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Sellevate.Gateway.Tests;

/// <summary>
/// Boots the gateway in-process and reads its live reverse-proxy configuration to prove
/// the <c>/friends/*</c>, <c>/discuss/*</c>, <c>/admin/discuss/*</c> and <c>/chat/*</c>
/// slices have been flipped off the monolith and onto the dedicated social cluster (Phase 5.4).
/// </summary>
[TestFixture]
public class SocialRouteFlipTests
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
    public void Friends_route_targets_the_social_cluster()
    {
        _configuration["ReverseProxy:Routes:social-friends:ClusterId"].Should().Be("social");
        _configuration["ReverseProxy:Routes:social-friends:Match:Path"].Should().Be("/friends/{**catch-all}");
    }

    [Test]
    public void Discuss_route_targets_the_social_cluster()
    {
        _configuration["ReverseProxy:Routes:social-discuss:ClusterId"].Should().Be("social");
        _configuration["ReverseProxy:Routes:social-discuss:Match:Path"].Should().Be("/discuss/{**catch-all}");
    }

    [Test]
    public void Admin_discuss_route_targets_the_social_cluster()
    {
        _configuration["ReverseProxy:Routes:social-admin-discuss:ClusterId"].Should().Be("social");
        _configuration["ReverseProxy:Routes:social-admin-discuss:Match:Path"].Should().Be("/admin/discuss/{**catch-all}");
    }

    [Test]
    public void Chat_route_targets_the_social_cluster()
    {
        _configuration["ReverseProxy:Routes:social-chat:ClusterId"].Should().Be("social");
        _configuration["ReverseProxy:Routes:social-chat:Match:Path"].Should().Be("/chat/{**catch-all}");
    }

    [Test]
    public void Social_cluster_has_a_destination()
    {
        _configuration["ReverseProxy:Clusters:social:Destinations:d1:Address"]
            .Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public void Social_slices_are_no_longer_served_by_the_monolith()
    {
        _configuration["ReverseProxy:Routes:social-friends:ClusterId"].Should().NotBe("monolith");
        _configuration["ReverseProxy:Routes:social-discuss:ClusterId"].Should().NotBe("monolith");
        _configuration["ReverseProxy:Routes:social-chat:ClusterId"].Should().NotBe("monolith");
    }
}
