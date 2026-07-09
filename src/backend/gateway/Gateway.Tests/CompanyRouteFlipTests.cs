using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Sellevate.Gateway.Tests;

/// <summary>
/// Boots the gateway in-process and reads its live reverse-proxy configuration to prove
/// the <c>/companies/*</c> slice is routed to the dedicated company cluster (Phase 39.4).
/// </summary>
[TestFixture]
public class CompanyRouteFlipTests
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
    public void Companies_collection_route_targets_the_company_cluster()
    {
        _configuration["ReverseProxy:Routes:company-companies:ClusterId"].Should().Be("company");
        _configuration["ReverseProxy:Routes:company-companies:Match:Path"]
            .Should().Be("/companies/{**catch-all}");
    }

    [Test]
    public void Companies_root_route_targets_the_company_cluster()
    {
        _configuration["ReverseProxy:Routes:company-companies-root:ClusterId"].Should().Be("company");
        _configuration["ReverseProxy:Routes:company-companies-root:Match:Path"].Should().Be("/companies");
    }

    [Test]
    public void Company_cluster_has_a_destination()
    {
        _configuration["ReverseProxy:Clusters:company:Destinations:d1:Address"]
            .Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public void Companies_are_not_served_by_the_monolith()
    {
        _configuration["ReverseProxy:Routes:company-companies:ClusterId"].Should().NotBe("monolith");
    }

    [Test]
    public async Task Unknown_route_still_returns_404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/this/route/does/not/exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
