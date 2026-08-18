using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Sellevate.Identity.Common.Constants;

namespace Sellevate.Identity.Tests.Unit;

/// <summary>
/// Exercises the four policies of the 2026-08-16 role split against the real
/// <see cref="IAuthorizationService"/>, not against a re-implementation of the rules. Every claim
/// shape below is one the token minter can actually produce, and the two interesting ones are the
/// asymmetries the owner asked for: a platform administrator passes the organization-scoped gates
/// while holding no <c>org_role</c> claim at all, and a tenancy admin is refused the one thing —
/// adding and removing users — that separates it from a tenancy superadmin.
/// </summary>
[TestFixture]
public sealed class AuthorizationPolicyTests
{
    private IAuthorizationService _authorizationService = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(AuthorizationPolicies.Register);
        _authorizationService = services.BuildServiceProvider()
            .GetRequiredService<IAuthorizationService>();
    }

    private async Task<bool> IsAllowedAsync(ClaimsPrincipal principal, string policyName)
    {
        var authorizationResult = await _authorizationService.AuthorizeAsync(
            principal, resource: null, policyName);
        return authorizationResult.Succeeded;
    }

    /// <summary>
    /// The role claim type is spelled out on the identity because that is what the JWT handler's
    /// inbound mapping produces, and <c>RequireRole</c>/<c>IsInRole</c> answer against it.
    /// </summary>
    private static ClaimsPrincipal BuildPrincipal(string? platformRole, string? organizationRole)
    {
        var claims = new List<Claim>();
        if (platformRole is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, platformRole));
        }

        if (organizationRole is not null)
        {
            claims.Add(new Claim(AuthorizationPolicies.OrganizationRoleClaimType, organizationRole));
        }

        return new ClaimsPrincipal(
            new ClaimsIdentity(claims, "TestAuthentication", ClaimTypes.Name, ClaimTypes.Role));
    }

    private static ClaimsPrincipal PlatformUser => BuildPrincipal("User", organizationRole: null);

    private static ClaimsPrincipal PlatformAdministrator
        => BuildPrincipal(AuthorizationPolicies.AdministratorRole, organizationRole: null);

    private static ClaimsPrincipal PlatformSuperAdministrator
        => BuildPrincipal(AuthorizationPolicies.SuperAdministratorRole, organizationRole: null);

    private static ClaimsPrincipal OrganizationManager
        => BuildPrincipal("User", "Manager");

    private static ClaimsPrincipal TenancyAdministrator
        => BuildPrincipal("User", AuthorizationPolicies.TenancyAdministratorOrganizationRole);

    private static ClaimsPrincipal TenancySuperAdministrator
        => BuildPrincipal("User", AuthorizationPolicies.TenancySuperAdministratorOrganizationRole);

    [Test]
    public async Task RequirePlatformAdmin_admits_both_platform_staff_roles_and_nobody_else()
    {
        (await IsAllowedAsync(PlatformAdministrator, AuthorizationPolicies.RequirePlatformAdministrator))
            .Should().BeTrue();
        (await IsAllowedAsync(PlatformSuperAdministrator, AuthorizationPolicies.RequirePlatformAdministrator))
            .Should().BeTrue();

        (await IsAllowedAsync(PlatformUser, AuthorizationPolicies.RequirePlatformAdministrator))
            .Should().BeFalse();
        (await IsAllowedAsync(OrganizationManager, AuthorizationPolicies.RequirePlatformAdministrator))
            .Should().BeFalse();
        (await IsAllowedAsync(TenancySuperAdministrator, AuthorizationPolicies.RequirePlatformAdministrator))
            .Should().BeFalse("an organization's own superadmin runs one tenancy, never the platform");
    }

    [Test]
    public async Task RequireSuperAdmin_admits_only_the_platform_super_admin()
    {
        (await IsAllowedAsync(PlatformSuperAdministrator, AuthorizationPolicies.RequireSuperAdministrator))
            .Should().BeTrue();

        (await IsAllowedAsync(PlatformAdministrator, AuthorizationPolicies.RequireSuperAdministrator))
            .Should().BeFalse("adding and removing users is the one thing a platform Admin may not do");
        (await IsAllowedAsync(TenancySuperAdministrator, AuthorizationPolicies.RequireSuperAdministrator))
            .Should().BeFalse();
        (await IsAllowedAsync(PlatformUser, AuthorizationPolicies.RequireSuperAdministrator))
            .Should().BeFalse();
    }

    [Test]
    public async Task RequireOrgAdmin_admits_both_tenancy_admin_roles()
    {
        (await IsAllowedAsync(TenancyAdministrator, AuthorizationPolicies.RequireOrganizationAdministrator))
            .Should().BeTrue();
        (await IsAllowedAsync(TenancySuperAdministrator, AuthorizationPolicies.RequireOrganizationAdministrator))
            .Should().BeTrue();

        (await IsAllowedAsync(OrganizationManager, AuthorizationPolicies.RequireOrganizationAdministrator))
            .Should().BeFalse();
        (await IsAllowedAsync(PlatformUser, AuthorizationPolicies.RequireOrganizationAdministrator))
            .Should().BeFalse();
    }

    [Test]
    public async Task RequireOrgAdmin_admits_platform_staff_carrying_no_org_role_claim()
    {
        PlatformAdministrator.Claims
            .Should().NotContain(claim => claim.Type == AuthorizationPolicies.OrganizationRoleClaimType,
                "the fixture must genuinely have no org_role claim for this to mean anything");

        (await IsAllowedAsync(PlatformAdministrator, AuthorizationPolicies.RequireOrganizationAdministrator))
            .Should().BeTrue("Sellevate staff normally hold no membership anywhere and must still work");
        (await IsAllowedAsync(PlatformSuperAdministrator, AuthorizationPolicies.RequireOrganizationAdministrator))
            .Should().BeTrue();
    }

    [Test]
    public async Task RequireOrgSuperAdmin_refuses_a_tenancy_admin()
    {
        (await IsAllowedAsync(TenancyAdministrator, AuthorizationPolicies.RequireOrganizationSuperAdministrator))
            .Should().BeFalse(
                "adding and removing users is the single privilege separating the two tenancy roles");
        (await IsAllowedAsync(OrganizationManager, AuthorizationPolicies.RequireOrganizationSuperAdministrator))
            .Should().BeFalse();
        (await IsAllowedAsync(PlatformUser, AuthorizationPolicies.RequireOrganizationSuperAdministrator))
            .Should().BeFalse();
    }

    [Test]
    public async Task RequireOrgSuperAdmin_admits_a_tenancy_super_admin_and_the_platform_super_admin()
    {
        (await IsAllowedAsync(TenancySuperAdministrator, AuthorizationPolicies.RequireOrganizationSuperAdministrator))
            .Should().BeTrue();
        (await IsAllowedAsync(PlatformSuperAdministrator, AuthorizationPolicies.RequireOrganizationSuperAdministrator))
            .Should().BeTrue();
    }

    [Test]
    public async Task RequireOrgSuperAdmin_refuses_a_platform_admin()
    {
        (await IsAllowedAsync(PlatformAdministrator, AuthorizationPolicies.RequireOrganizationSuperAdministrator))
            .Should().BeFalse(
                "a platform Admin reaches everything inside an organization except adding and "
                + "removing its users — that is what makes it an Admin rather than a SuperAdmin");
    }

    [Test]
    public async Task An_unauthenticated_principal_passes_nothing()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        foreach (var policyName in new[]
                 {
                     AuthorizationPolicies.RequirePlatformAdministrator,
                     AuthorizationPolicies.RequireSuperAdministrator,
                     AuthorizationPolicies.RequireOrganizationAdministrator,
                     AuthorizationPolicies.RequireOrganizationSuperAdministrator
                 })
        {
            (await IsAllowedAsync(anonymous, policyName)).Should().BeFalse(policyName);
        }
    }
}
