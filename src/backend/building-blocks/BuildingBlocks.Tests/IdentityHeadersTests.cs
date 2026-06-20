using System.Security.Claims;
using FluentAssertions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Identity;

namespace Sellevate.BuildingBlocks.Tests;

[TestFixture]
public class IdentityHeadersTests
{
    [Test]
    public void ResolveUserId_reads_the_sub_claim()
    {
        var principal = PrincipalWith(new Claim("sub", "user-123"));

        IdentityHeaders.ResolveUserId(principal).Should().Be("user-123");
    }

    [Test]
    public void ResolveUserId_falls_back_to_name_identifier()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, "user-456"));

        IdentityHeaders.ResolveUserId(principal).Should().Be("user-456");
    }

    [Test]
    public void ResolveRole_reads_the_role_claim()
    {
        var principal = PrincipalWith(new Claim(ClaimTypes.Role, "Admin"));

        IdentityHeaders.ResolveRole(principal).Should().Be("Admin");
    }

    [Test]
    public void Resolvers_return_null_for_an_anonymous_principal()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        IdentityHeaders.ResolveUserId(anonymous).Should().BeNull();
        IdentityHeaders.ResolveRole(anonymous).Should().BeNull();
    }

    private static ClaimsPrincipal PrincipalWith(params Claim[] claims)
        => new(new ClaimsIdentity(claims, authenticationType: "test"));
}
