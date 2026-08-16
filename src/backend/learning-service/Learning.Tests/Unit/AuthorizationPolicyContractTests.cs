using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Sellevate.Learning.Common.Constants;

namespace Sellevate.Learning.Tests.Unit;

/// <summary>
/// The 2026-08-16 role split declares the same four policies in every service, which only holds if
/// each service actually registers them and spells the names the same way. Both halves are easy to
/// break silently — a service whose `Program.cs` forgets `AuthorizationPolicies.Register` fails
/// closed on every gated route, and a renamed constant makes a token mean two different things in
/// two services. This pins the wire-level names and the behaviour that matters most: a platform
/// administrator reaching organization-scoped routes while carrying no `org_role` claim.
/// </summary>
[TestFixture]
public sealed class AuthorizationPolicyContractTests
{
    private IAuthorizationService authorizationService = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(AuthorizationPolicies.Register);
        authorizationService = services.BuildServiceProvider()
            .GetRequiredService<IAuthorizationService>();
    }

    private async Task<bool> IsAllowedAsync(ClaimsPrincipal principal, string policyName)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(
            principal, resource: null, policyName);
        return authorizationResult.Succeeded;
    }

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

    [Test]
    public void The_policy_and_role_names_match_the_platform_contract()
    {
        AuthorizationPolicies.RequirePlatformAdministrator.Should().Be("RequirePlatformAdmin");
        AuthorizationPolicies.RequireSuperAdministrator.Should().Be("RequireSuperAdmin");
        AuthorizationPolicies.RequireOrganizationAdministrator.Should().Be("RequireOrgAdmin");
        AuthorizationPolicies.RequireOrganizationSuperAdministrator.Should().Be("RequireOrgSuperAdmin");

        AuthorizationPolicies.AdministratorRole.Should().Be("Admin");
        AuthorizationPolicies.SuperAdministratorRole.Should().Be("SuperAdmin");
        AuthorizationPolicies.TenancyAdministratorOrganizationRole.Should().Be("TenancyAdmin");
        AuthorizationPolicies.TenancySuperAdministratorOrganizationRole.Should().Be("TenancySuperAdmin");
        AuthorizationPolicies.OrganizationRoleClaimType.Should().Be("org_role");
    }

    [Test]
    public async Task All_four_policies_are_registered_and_resolvable()
    {
        var anyPrincipal = BuildPrincipal(AuthorizationPolicies.SuperAdministratorRole, organizationRole: null);

        foreach (var policyName in new[]
                 {
                     AuthorizationPolicies.RequirePlatformAdministrator,
                     AuthorizationPolicies.RequireSuperAdministrator,
                     AuthorizationPolicies.RequireOrganizationAdministrator,
                     AuthorizationPolicies.RequireOrganizationSuperAdministrator
                 })
        {
            var isAllowed = await IsAllowedAsync(anyPrincipal, policyName);
            isAllowed.Should().BeTrue(
                "a platform SuperAdmin satisfies every policy; a false here means {0} is not "
                + "registered at all, since an unregistered policy name throws or fails closed",
                policyName);
        }
    }

    [Test]
    public async Task A_platform_admin_passes_the_org_scoped_gate_without_an_org_role_claim()
    {
        var platformAdministrator = BuildPrincipal(
            AuthorizationPolicies.AdministratorRole, organizationRole: null);

        (await IsAllowedAsync(platformAdministrator, AuthorizationPolicies.RequireOrganizationAdministrator))
            .Should().BeTrue();
        (await IsAllowedAsync(platformAdministrator, AuthorizationPolicies.RequirePlatformAdministrator))
            .Should().BeTrue();
        (await IsAllowedAsync(platformAdministrator, AuthorizationPolicies.RequireSuperAdministrator))
            .Should().BeFalse();
    }

    [Test]
    public async Task A_tenancy_admin_is_refused_the_add_and_remove_users_gate()
    {
        var tenancyAdministrator = BuildPrincipal(
            "User", AuthorizationPolicies.TenancyAdministratorOrganizationRole);

        (await IsAllowedAsync(tenancyAdministrator, AuthorizationPolicies.RequireOrganizationAdministrator))
            .Should().BeTrue();
        (await IsAllowedAsync(tenancyAdministrator, AuthorizationPolicies.RequireOrganizationSuperAdministrator))
            .Should().BeFalse();
    }

    [Test]
    public async Task An_ordinary_user_passes_nothing()
    {
        var ordinaryUser = BuildPrincipal("User", organizationRole: null);

        foreach (var policyName in new[]
                 {
                     AuthorizationPolicies.RequirePlatformAdministrator,
                     AuthorizationPolicies.RequireSuperAdministrator,
                     AuthorizationPolicies.RequireOrganizationAdministrator,
                     AuthorizationPolicies.RequireOrganizationSuperAdministrator
                 })
        {
            (await IsAllowedAsync(ordinaryUser, policyName)).Should().BeFalse(policyName);
        }
    }
}
