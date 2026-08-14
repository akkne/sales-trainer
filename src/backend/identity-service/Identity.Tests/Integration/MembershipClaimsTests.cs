using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Infrastructure.Data;
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
        const string password = "Password123!";

        var register = await client.PostAsJsonAsync("/auth/register",
            new { email, password, displayName = "Member User" });
        register.StatusCode.Should().Be(HttpStatusCode.OK);
        var registered = await register.Content.ReadFromJsonAsync<AuthTokenResult>();

        var organizationId = Guid.NewGuid();
        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            database.Memberships.Add(new Membership
            {
                UserId = Guid.Parse(registered!.UserId),
                OrganizationId = organizationId,
                Role = OrgRole.OrgAdmin,
                Status = MembershipStatus.Active,
                JoinedAt = DateTime.UtcNow
            });
            await database.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/auth/login", new { email, password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await login.Content.ReadFromJsonAsync<AuthTokenResult>();

        body!.OrgId.Should().Be(organizationId.ToString());
        body.OrgRole.Should().Be("OrgAdmin");

        var claims = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken).Claims.ToList();
        claims.Should().ContainSingle(c => c.Type == "org_id" && c.Value == organizationId.ToString());
        claims.Should().ContainSingle(c => c.Type == "org_role" && c.Value == "OrgAdmin");
    }

    [Test]
    public async Task Login_WithoutMembership_OmitsOrgClaims()
    {
        var client = Factory.CreateClient();
        var email = UniqueEmail();
        const string password = "Password123!";

        await client.PostAsJsonAsync("/auth/register", new { email, password, displayName = "No Org" });

        var login = await client.PostAsJsonAsync("/auth/login", new { email, password });
        var body = await login.Content.ReadFromJsonAsync<AuthTokenResult>();

        body!.OrgId.Should().BeNull();
        body.OrgRole.Should().BeNull();

        var claims = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken).Claims.ToList();
        claims.Should().NotContain(c => c.Type == "org_id");
        claims.Should().NotContain(c => c.Type == "org_role");
    }

    [Test]
    public async Task Login_WithDeactivatedMembership_OmitsOrgClaims()
    {
        var client = Factory.CreateClient();
        var email = UniqueEmail();
        const string password = "Password123!";

        var register = await client.PostAsJsonAsync("/auth/register",
            new { email, password, displayName = "Deactivated Member" });
        var registered = await register.Content.ReadFromJsonAsync<AuthTokenResult>();

        using (var scope = Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            database.Memberships.Add(new Membership
            {
                UserId = Guid.Parse(registered!.UserId),
                OrganizationId = Guid.NewGuid(),
                Role = OrgRole.Manager,
                Status = MembershipStatus.Deactivated,
                JoinedAt = DateTime.UtcNow.AddDays(-30),
                DeactivatedAt = DateTime.UtcNow
            });
            await database.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/auth/login", new { email, password });
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
