using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace Sellevate.Gateway.Tests;

[TestFixture]
public class LearningRouteFlipTests
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

    [TestCase("learning-skills", "/skills/{**catch-all}")]
    [TestCase("learning-skill-tree", "/skill-tree")]
    [TestCase("learning-lessons", "/lessons/{**catch-all}")]
    [TestCase("learning-topics", "/topics/{**catch-all}")]
    [TestCase("learning-exercises", "/exercises/{**catch-all}")]
    [TestCase("learning-reference", "/reference/{**catch-all}")]
    [TestCase("learning-techniques", "/techniques/{**catch-all}")]
    [TestCase("learning-daily-quote", "/daily-quote")]
    [TestCase("learning-admin-skills", "/admin/skills/{**catch-all}")]
    [TestCase("learning-admin-exercise-type-prompts", "/admin/exercise-type-prompts/{**catch-all}")]
    [TestCase("learning-admin-seeder", "/admin/seeder/{**catch-all}")]
    public void Learning_route_targets_the_learning_cluster(string routeName, string expectedPath)
    {
        _configuration[$"ReverseProxy:Routes:{routeName}:ClusterId"].Should().Be("learning");
        _configuration[$"ReverseProxy:Routes:{routeName}:Match:Path"].Should().Be(expectedPath);
    }

    [Test]
    public void Learning_cluster_has_a_destination()
    {
        _configuration["ReverseProxy:Clusters:learning:Destinations:d1:Address"]
            .Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public void Learning_slices_are_no_longer_served_by_the_monolith()
    {
        _configuration["ReverseProxy:Routes:learning-skills:ClusterId"].Should().NotBe("monolith");
        _configuration["ReverseProxy:Routes:learning-exercises:ClusterId"].Should().NotBe("monolith");
        _configuration["ReverseProxy:Routes:learning-techniques:ClusterId"].Should().NotBe("monolith");
    }

    [Test]
    public void Profile_routes_are_not_captured_by_learning()
    {
        _configuration["ReverseProxy:Routes:learning-skills:Match:Path"].Should().NotContain("/profile");
    }
}
