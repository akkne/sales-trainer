using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace Sellevate.Gateway.Tests;

[TestFixture]
public class IdentityAdminUsersRouteFlipTests
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

    [TestCase("identity-admin-users", "/admin/users/{**catch-all}")]
    [TestCase("identity-admin-users-root", "/admin/users")]
    public void Admin_users_route_targets_the_identity_cluster(string routeName, string expectedPath)
    {
        _configuration[$"ReverseProxy:Routes:{routeName}:ClusterId"].Should().Be("identity");
        _configuration[$"ReverseProxy:Routes:{routeName}:Match:Path"].Should().Be(expectedPath);
    }

    [Test]
    public void Admin_users_is_no_longer_served_by_the_monolith()
    {
        _configuration["ReverseProxy:Routes:identity-admin-users:ClusterId"].Should().NotBe("monolith");
        _configuration["ReverseProxy:Routes:identity-admin-users-root:ClusterId"].Should().NotBe("monolith");
    }
}
