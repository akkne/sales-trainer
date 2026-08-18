using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Auth.Services.Abstract;
using Sellevate.Identity.Features.Auth.Services.Implementation;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Infrastructure.Configuration;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Unit;

/// <summary>
/// The policies of the 2026-08-16 role split match on claim text, so what the minter writes into
/// the token is the other half of the contract asserted in <see cref="AuthorizationPolicyTests"/>.
/// These read the issued JWT rather than the DTO, because the DTO is what the frontend sees while
/// the claims are what every service authorizes against, and only one of the two can be trusted
/// to be the thing under test.
/// </summary>
[TestFixture]
public sealed class TokenRoleClaimTests
{
    /// <summary>
    /// The minter writes <see cref="ClaimTypes.Role"/>, but <c>JwtSecurityTokenHandler</c>'s
    /// outbound map shortens it to <c>role</c> on the wire — and <c>role</c> is what these tests
    /// assert, because it is what actually travels. The receiving services see it back as
    /// <see cref="ClaimTypes.Role"/> after the symmetric inbound map, which is why
    /// <c>RequireRole</c> works against it.
    /// </summary>
    private const string PlatformRoleClaimType = "role";

    private static readonly IOptions<JwtConfiguration> JwtOptions = Options.Create(new JwtConfiguration
    {
        Key = "super-secret-test-key-that-is-at-least-32-bytes",
        Issuer = "test",
        Audience = "test",
        AccessTokenLifetimeMinutes = 15,
        RefreshTokenLifetimeDays = 7,
        DemoTokenLifetimeHours = 1
    });

    private static AuthenticationService BuildService(IdentityDbContext databaseContext) =>
        new(
            databaseContext,
            Substitute.For<IEmailVerificationService>(),
            Substitute.For<IGoogleTokenValidator>(),
            new OrganizationAuthConfigurationResolver(
                databaseContext, NullLogger<OrganizationAuthConfigurationResolver>.Instance),
            [new PasswordAuthProvider(databaseContext, NullLogger<PasswordAuthProvider>.Instance)],
            JwtOptions,
            NullLogger<AuthenticationService>.Instance);

    private static async Task<User> SeedUserAsync(IdentityDbContext databaseContext, UserRole role)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"{role}-{Guid.NewGuid():N}@test.com",
            DisplayName = "Claim Subject",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = true,
            Role = role,
            AvatarType = AvatarKind.Default
        };
        databaseContext.Users.Add(user);
        await databaseContext.SaveChangesAsync();
        return user;
    }

    private static async Task SeedActiveMembershipAsync(
        IdentityDbContext databaseContext, Guid userId, Guid organizationId, OrgRole organizationRole)
    {
        databaseContext.Memberships.Add(new Membership
        {
            UserId = userId,
            OrganizationId = organizationId,
            Role = organizationRole,
            Status = MembershipStatus.Active,
            JoinedAt = DateTime.UtcNow
        });
        await databaseContext.SaveChangesAsync();
    }

    private static IReadOnlyList<Claim> ReadClaims(string accessToken)
        => new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims.ToList();

    private static string? SingleClaimValue(IReadOnlyList<Claim> claims, string claimType)
        => claims.SingleOrDefault(claim => claim.Type == claimType)?.Value;

    [TestCase(UserRole.User, "User")]
    [TestCase(UserRole.Admin, "Admin")]
    [TestCase(UserRole.SuperAdmin, "SuperAdmin")]
    public async Task The_platform_role_is_issued_verbatim(UserRole role, string expectedClaimValue)
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var user = await SeedUserAsync(databaseContext, role);

        var issuedTokenPair = await BuildService(databaseContext).IssueTokensForUserAsync(user);

        SingleClaimValue(ReadClaims(issuedTokenPair.AccessToken), PlatformRoleClaimType)
            .Should().Be(expectedClaimValue);
    }

    [TestCase(OrgRole.Manager, "Manager")]
    [TestCase(OrgRole.TenancyAdmin, "TenancyAdmin")]
    [TestCase(OrgRole.TenancySuperAdmin, "TenancySuperAdmin")]
    public async Task The_organization_role_is_issued_verbatim(
        OrgRole organizationRole, string expectedClaimValue)
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var user = await SeedUserAsync(databaseContext, UserRole.User);
        var organizationId = Guid.NewGuid();
        await SeedActiveMembershipAsync(databaseContext, user.Id, organizationId, organizationRole);

        var issuedTokenPair = await BuildService(databaseContext).IssueTokensForUserAsync(user);

        var claims = ReadClaims(issuedTokenPair.AccessToken);
        SingleClaimValue(claims, "org_role").Should().Be(expectedClaimValue);
        SingleClaimValue(claims, "org_id").Should().Be(organizationId.ToString());
        issuedTokenPair.OrgRole.Should().Be(expectedClaimValue);
    }

    [TestCase(UserRole.Admin)]
    [TestCase(UserRole.SuperAdmin)]
    public async Task Platform_staff_without_a_membership_get_no_org_claims_at_all(UserRole role)
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var user = await SeedUserAsync(databaseContext, role);

        var issuedTokenPair = await BuildService(databaseContext).IssueTokensForUserAsync(user);

        var claims = ReadClaims(issuedTokenPair.AccessToken);
        claims.Should().NotContain(claim => claim.Type == "org_id");
        claims.Should().NotContain(claim => claim.Type == "org_role",
            "absent membership is never implied — the platform routes are reached on the platform "
            + "role alone");
        issuedTokenPair.OrgId.Should().BeNull();
        issuedTokenPair.OrgRole.Should().BeNull();
    }

    [Test]
    public async Task A_platform_admin_with_a_membership_carries_both_axes_independently()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var user = await SeedUserAsync(databaseContext, UserRole.Admin);
        await SeedActiveMembershipAsync(
            databaseContext, user.Id, Guid.NewGuid(), OrgRole.Manager);

        var issuedTokenPair = await BuildService(databaseContext).IssueTokensForUserAsync(user);

        var claims = ReadClaims(issuedTokenPair.AccessToken);
        SingleClaimValue(claims, PlatformRoleClaimType).Should().Be("Admin");
        SingleClaimValue(claims, "org_role").Should().Be("Manager",
            "the platform role never overwrites the organization role — they are two axes");
    }
}
