using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NUnit.Framework;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Integration;

// Phase 40.6: the JWT must carry `org_id`/`org_role` for a user with an active membership,
// and must carry neither for a user without one — absent membership is never implicit
// organization access.
[TestFixture]
[Category("Integration")]
public class MembershipClaimsTests
{
    private TestWebApplicationFactory Factory => IntegrationTestSetup.Factory;

    private static string UniqueEmail() => $"member-{Guid.NewGuid():N}@test.com";

    [Test]
    public async Task Login_WithActiveMembership_IncludesOrgClaims()
    {
        var client = Factory.CreateClient();
        var email = UniqueEmail();
        var organizationId = Guid.NewGuid();
        await TestUserSeeder.SeedUserAsync(
            Factory, email, "Member User", organizationId: organizationId, organizationRole: OrgRole.TenancyAdmin);

        var login = await client.PostAsJsonAsync("/auth/login",
            new { email, password = TestUserSeeder.DefaultPassword });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await login.Content.ReadFromJsonAsync<AuthTokenResult>();

        body!.OrgId.Should().Be(organizationId.ToString());
        body.OrgRole.Should().Be("TenancyAdmin");

        var claims = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken).Claims.ToList();
        claims.Should().ContainSingle(claim => claim.Type == "org_id" && claim.Value == organizationId.ToString());
        claims.Should().ContainSingle(claim => claim.Type == "org_role" && claim.Value == "TenancyAdmin");
    }

    [Test]
    public async Task Login_WithoutMembership_OmitsOrgClaims()
    {
        var client = Factory.CreateClient();
        var email = UniqueEmail();
        await TestUserSeeder.SeedUserAsync(Factory, email, "No Org");

        var login = await client.PostAsJsonAsync("/auth/login",
            new { email, password = TestUserSeeder.DefaultPassword });
        var body = await login.Content.ReadFromJsonAsync<AuthTokenResult>();

        body!.OrgId.Should().BeNull();
        body.OrgRole.Should().BeNull();

        var claims = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken).Claims.ToList();
        claims.Should().NotContain(claim => claim.Type == "org_id");
        claims.Should().NotContain(claim => claim.Type == "org_role");
    }

    [Test]
    public async Task Login_WithDeactivatedMembership_OmitsOrgClaims()
    {
        var client = Factory.CreateClient();
        var email = UniqueEmail();
        await TestUserSeeder.SeedUserAsync(
            Factory,
            email,
            "Deactivated Member",
            organizationId: Guid.NewGuid(),
            membershipStatus: MembershipStatus.Deactivated);

        var login = await client.PostAsJsonAsync("/auth/login",
            new { email, password = TestUserSeeder.DefaultPassword });
        var body = await login.Content.ReadFromJsonAsync<AuthTokenResult>();

        body!.OrgId.Should().BeNull();
        body.OrgRole.Should().BeNull();
    }

    private sealed record AuthTokenResult(
        string AccessToken,
        string UserId,
        string DisplayName,
        bool IsOnboardingCompleted,
        string Role,
        string? OrgId,
        string? OrgRole);
}
