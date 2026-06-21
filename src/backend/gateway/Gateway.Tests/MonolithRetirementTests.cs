using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace Sellevate.Gateway.Tests;

/// <summary>
/// Proves the monolith is retired at the gateway: no monolith cluster, no
/// {**catch-all} route, and genuinely-unknown routes now 404 instead of silently
/// falling through to the monolith.
/// </summary>
[TestFixture]
public class MonolithRetirementTests
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
    public void No_monolith_cluster_is_configured()
    {
        _configuration["ReverseProxy:Clusters:monolith:Destinations:d1:Address"]
            .Should().BeNull();
    }

    [Test]
    public void No_catch_all_route_is_configured()
    {
        var routesSection = _configuration.GetSection("ReverseProxy:Routes");

        foreach (var route in routesSection.GetChildren())
        {
            var path = route["Match:Path"];
            path.Should().NotBe("{**catch-all}",
                because: $"route '{route.Key}' must not be a bare catch-all to the monolith");
            route["ClusterId"].Should().NotBe("monolith",
                because: $"route '{route.Key}' must not target the retired monolith");
        }
    }

    [TestCase("/admin/users", "identity")]
    [TestCase("/admin/users/{**catch-all}", "identity")]
    [TestCase("/admin/skills/{**catch-all}", "learning")]
    [TestCase("/admin/lessons/{**catch-all}", "learning")]
    [TestCase("/admin/exercises/{**catch-all}", "learning")]
    [TestCase("/admin/topics/{**catch-all}", "learning")]
    [TestCase("/admin/reference/{**catch-all}", "learning")]
    [TestCase("/admin/techniques/{**catch-all}", "learning")]
    [TestCase("/admin/daily-quotes/{**catch-all}", "learning")]
    [TestCase("/admin/exercise-type-prompts/{**catch-all}", "learning")]
    [TestCase("/admin/skill-stages/{**catch-all}", "learning")]
    [TestCase("/admin/seeder/{**catch-all}", "learning")]
    [TestCase("/admin/gamification/{**catch-all}", "gamification")]
    [TestCase("/admin/leagues/{**catch-all}", "gamification")]
    [TestCase("/admin/dialog/{**catch-all}", "ai")]
    [TestCase("/admin/voice/{**catch-all}", "ai")]
    [TestCase("/admin/discuss/{**catch-all}", "social")]
    public void Every_admin_route_maps_to_its_owning_service(string expectedPath, string expectedCluster)
    {
        var routesSection = _configuration.GetSection("ReverseProxy:Routes");

        var match = routesSection.GetChildren()
            .FirstOrDefault(route => route["Match:Path"] == expectedPath);

        match.Should().NotBeNull(because: $"a route for '{expectedPath}' must exist");
        match!["ClusterId"].Should().Be(expectedCluster);
    }

    [Test]
    public async Task Unknown_route_returns_404_with_no_catch_all()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/this/route/does/not/exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
