using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Sellevate.Identity.Features.Auth.Constants;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Auth.Services.Implementation;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Tests.Helpers;
using MembershipEntity = Sellevate.Identity.Features.Membership.Models.Membership;

namespace Sellevate.Identity.Tests.Unit;

/// <summary>
/// Phase 40.8 — step 2 of the three-step login flow: email → organization → configured method.
/// </summary>
[TestFixture]
public class OrganizationAuthConfigurationResolverTests
{
    [Test]
    public async Task ResolveForEmailAsync_ForUnknownEmail_ReturnsThePlatformDefault()
    {
        using var databaseContext = InMemoryDbContextFactory.Create();
        var resolver = CreateResolver(databaseContext);

        var resolved = await resolver.ResolveForEmailAsync("stranger@nowhere.test");

        resolved.Method.Should().Be(AuthMethodNames.Password);
        resolved.OrganizationId.Should().BeNull();
    }

    /// <summary>
    /// The anti-enumeration property that makes <c>POST /auth/login/start</c> safe to expose: an
    /// address nobody has ever heard of and an address belonging to a real password organization
    /// must produce the same answer.
    /// </summary>
    [Test]
    public async Task ResolveForEmailAsync_ForKnownAndUnknownPasswordAddresses_ReturnsTheSameMethod()
    {
        using var databaseContext = InMemoryDbContextFactory.Create();
        var organizationId = Guid.NewGuid();
        await SeedMemberAsync(databaseContext, "member@known.test", organizationId);
        await SeedConfigurationAsync(databaseContext, organizationId, AuthMethodNames.Password);
        var resolver = CreateResolver(databaseContext);

        var knownAddressResolution = await resolver.ResolveForEmailAsync("member@known.test");
        var unknownAddressResolution = await resolver.ResolveForEmailAsync("stranger@nowhere.test");

        knownAddressResolution.Method.Should().Be(unknownAddressResolution.Method);
    }

    [Test]
    public async Task ResolveForEmailAsync_ByAllowedEmailDomain_ReturnsThatOrganizationsMethod()
    {
        using var databaseContext = InMemoryDbContextFactory.Create();
        var organizationId = Guid.NewGuid();
        await SeedConfigurationAsync(
            databaseContext, organizationId, AuthMethodNames.Oidc, allowedEmailDomains: ["bigcustomer.test"]);
        var resolver = CreateResolver(databaseContext);

        var resolved = await resolver.ResolveForEmailAsync("Newcomer@BigCustomer.test");

        resolved.Method.Should().Be(AuthMethodNames.Oidc);
        resolved.OrganizationId.Should().Be(organizationId);
    }

    [Test]
    public async Task ResolveForEmailAsync_ForAnUnclaimedDomain_ReturnsThePlatformDefault()
    {
        using var databaseContext = InMemoryDbContextFactory.Create();
        await SeedConfigurationAsync(
            databaseContext, Guid.NewGuid(), AuthMethodNames.Oidc, allowedEmailDomains: ["bigcustomer.test"]);
        var resolver = CreateResolver(databaseContext);

        var resolved = await resolver.ResolveForEmailAsync("someone@othercompany.test");

        resolved.Method.Should().Be(AuthMethodNames.Password);
        resolved.OrganizationId.Should().BeNull();
    }

    /// <summary>
    /// The membership branch is the roadmap's "resolve by invite": someone who accepted an invite
    /// belongs to a known organization regardless of which mailbox provider their address uses,
    /// which is a stronger signal than a shared domain (gmail.com is not a customer).
    /// </summary>
    [Test]
    public async Task ResolveForEmailAsync_PrefersActiveMembershipOverEmailDomain()
    {
        using var databaseContext = InMemoryDbContextFactory.Create();
        var membershipOrganizationId = Guid.NewGuid();
        var domainOrganizationId = Guid.NewGuid();
        await SeedMemberAsync(databaseContext, "contractor@shared.test", membershipOrganizationId);
        await SeedConfigurationAsync(databaseContext, membershipOrganizationId, AuthMethodNames.Password);
        await SeedConfigurationAsync(
            databaseContext, domainOrganizationId, AuthMethodNames.Oidc, allowedEmailDomains: ["shared.test"]);
        var resolver = CreateResolver(databaseContext);

        var resolved = await resolver.ResolveForEmailAsync("contractor@shared.test");

        resolved.OrganizationId.Should().Be(membershipOrganizationId);
        resolved.Method.Should().Be(AuthMethodNames.Password);
    }

    [Test]
    public async Task ResolveForEmailAsync_IgnoresDeactivatedMemberships()
    {
        using var databaseContext = InMemoryDbContextFactory.Create();
        var organizationId = Guid.NewGuid();
        await SeedMemberAsync(
            databaseContext, "leaver@known.test", organizationId, MembershipStatus.Deactivated);
        await SeedConfigurationAsync(databaseContext, organizationId, AuthMethodNames.Oidc);
        var resolver = CreateResolver(databaseContext);

        var resolved = await resolver.ResolveForEmailAsync("leaver@known.test");

        resolved.OrganizationId.Should().BeNull();
        resolved.Method.Should().Be(AuthMethodNames.Password);
    }

    /// <summary>
    /// 40.9 creates organizations before anyone configures how they sign in. A member of such an
    /// organization must still be able to log in with a password.
    /// </summary>
    [Test]
    public async Task ResolveForEmailAsync_ForOrganizationWithoutConfiguration_FallsBackToPassword()
    {
        using var databaseContext = InMemoryDbContextFactory.Create();
        var organizationId = Guid.NewGuid();
        await SeedMemberAsync(databaseContext, "member@unconfigured.test", organizationId);
        var resolver = CreateResolver(databaseContext);

        var resolved = await resolver.ResolveForEmailAsync("member@unconfigured.test");

        resolved.OrganizationId.Should().Be(organizationId);
        resolved.Method.Should().Be(AuthMethodNames.Password);
    }

    private static OrganizationAuthConfigurationResolver CreateResolver(IdentityDbContext databaseContext)
        => new(databaseContext, NullLogger<OrganizationAuthConfigurationResolver>.Instance);

    private static async Task SeedMemberAsync(
        IdentityDbContext databaseContext,
        string email,
        Guid organizationId,
        MembershipStatus status = MembershipStatus.Active)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            DisplayName = "Member",
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = true
        };

        databaseContext.Users.Add(user);
        databaseContext.Memberships.Add(new MembershipEntity
        {
            UserId = user.Id,
            OrganizationId = organizationId,
            Role = OrgRole.Manager,
            Status = status,
            JoinedAt = DateTime.UtcNow,
            DeactivatedAt = status == MembershipStatus.Deactivated ? DateTime.UtcNow : null
        });
        await databaseContext.SaveChangesAsync();
    }

    private static async Task SeedConfigurationAsync(
        IdentityDbContext databaseContext,
        Guid organizationId,
        string method,
        string[]? allowedEmailDomains = null)
    {
        databaseContext.OrganizationAuthConfigurations.Add(new OrganizationAuthConfiguration
        {
            OrganizationId = organizationId,
            Method = method,
            AllowedEmailDomains = allowedEmailDomains ?? [],
            CreatedAt = DateTime.UtcNow
        });
        await databaseContext.SaveChangesAsync();
    }
}
