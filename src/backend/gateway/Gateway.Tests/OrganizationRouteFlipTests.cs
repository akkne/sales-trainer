using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Sellevate.Gateway.Tests;

/// <summary>
/// Boots the gateway in-process and reads its live reverse-proxy configuration to prove
/// the <c>/organizations/*</c> slice is routed to the dedicated organization cluster (Phase 40.5).
/// </summary>
[TestFixture]
public class OrganizationRouteFlipTests
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
    public void Organizations_collection_route_targets_the_organization_cluster()
    {
        _configuration["ReverseProxy:Routes:organization-organizations:ClusterId"].Should().Be("organization");
        _configuration["ReverseProxy:Routes:organization-organizations:Match:Path"]
            .Should().Be("/organizations/{**catch-all}");
    }

    [Test]
    public void Organizations_root_route_targets_the_organization_cluster()
    {
        _configuration["ReverseProxy:Routes:organization-organizations-root:ClusterId"].Should().Be("organization");
        _configuration["ReverseProxy:Routes:organization-organizations-root:Match:Path"].Should().Be("/organizations");
    }

    [Test]
    public void Organization_cluster_has_a_destination()
    {
        _configuration["ReverseProxy:Clusters:organization:Destinations:d1:Address"]
            .Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public void Organizations_are_not_served_by_the_monolith()
    {
        _configuration["ReverseProxy:Routes:organization-organizations:ClusterId"].Should().NotBe("monolith");
    }
}
