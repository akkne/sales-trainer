using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Common.Constants;
using Sellevate.Identity.Features.Admin;
using Sellevate.Identity.Features.Invites.Endpoints;
using Sellevate.Identity.Features.Membership.Endpoints;
using Sellevate.Identity.Features.PlatformAdmin.Endpoints;

namespace Sellevate.Identity.Tests.Unit;

/// <summary>
/// Encodes the Phase 40.9 and 2026-08-16 access decisions as executable checks. All of them are
/// the kind of property that is invisible in a diff once someone edits an attribute: a platform
/// route silently losing its gate, gaining a <c>[TenantScoped]</c> one it must not have, or an
/// add/remove-a-user route quietly dropping from superadmin to admin.
/// </summary>
[TestFixture]
public class PlatformAdminControllerContractTests
{
    private static string? PolicyOf<TController>()
        => typeof(TController).GetCustomAttribute<AuthorizeAttribute>()?.Policy;

    private static string? PolicyOfAction<TController>(string actionName)
        => typeof(TController).GetMethod(actionName)!
            .GetCustomAttribute<AuthorizeAttribute>()?.Policy;

    [Test]
    public void PlatformAdminController_is_gated_by_RequireSuperAdmin()
    {
        PolicyOf<PlatformAdminController>().Should().Be(
            AuthorizationPolicies.RequireSuperAdministrator,
            "impersonation and bootstrapping an organization's first admin are both "
            + "superadmin-exclusive");
    }

    [Test]
    public void PlatformAdminController_is_not_tenant_scoped()
    {
        typeof(PlatformAdminController).GetCustomAttribute<TenantScopedAttribute>()
            .Should().BeNull(
                "these routes act on organizations rather than inside one, so requiring an "
                + "X-Organization-Id header would make them unreachable for the platform staff "
                + "they exist for");
    }

    [Test]
    public void InvitesController_reads_are_org_admin_and_writes_are_org_super_admin()
    {
        typeof(InvitesController).GetCustomAttribute<TenantScopedAttribute>().Should().NotBeNull();
        PolicyOf<InvitesController>().Should().Be(
            AuthorizationPolicies.RequireOrganizationAdministrator,
            "reading who has been invited is ordinary organization administration (Phase 40.20)");

        foreach (var mutatingAction in new[] { "CreateInvites", "RevokeInvite" })
        {
            PolicyOfAction<InvitesController>(mutatingAction).Should().Be(
                AuthorizationPolicies.RequireOrganizationSuperAdministrator,
                "inviting and revoking are add/remove-a-user, the one privilege the 2026-08-16 role "
                + "split reserves for a superadmin — and an action attribute is ANDed with the "
                + "controller's, so the looser controller gate cannot widen it");
        }
    }

    [Test]
    public void MembershipsController_reads_are_org_admin_and_offboarding_stays_org_super_admin()
    {
        typeof(MembershipsController).GetCustomAttribute<TenantScopedAttribute>().Should().NotBeNull();
        PolicyOf<MembershipsController>().Should().Be(
            AuthorizationPolicies.RequireOrganizationAdministrator,
            "a TenancyAdmin assigns work to these people and cannot do it blind (Phase 40.20)");

        PolicyOfAction<MembershipsController>("DeactivateMembership").Should().Be(
            AuthorizationPolicies.RequireOrganizationSuperAdministrator,
            "offboarding removes a user from the organization");
    }

    [Test]
    public void AdminUsersController_reads_are_platform_admin_and_writes_are_super_admin()
    {
        PolicyOf<AdminUsersController>().Should().Be(
            AuthorizationPolicies.RequirePlatformAdministrator,
            "viewing the roster is ordinary platform administration");

        foreach (var mutatingAction in new[] { "UpdateUser", "DeleteAvatar", "ChangeRole" })
        {
            PolicyOfAction<AdminUsersController>(mutatingAction).Should().Be(
                AuthorizationPolicies.RequireSuperAdministrator, mutatingAction);
        }
    }

    [Test]
    public void AdminUsersController_read_actions_carry_no_tighter_gate_of_their_own()
    {
        foreach (var readingAction in new[] { "GetAll", "GetById" })
        {
            PolicyOfAction<AdminUsersController>(readingAction).Should().BeNull(
                $"{readingAction} inherits the controller's RequirePlatformAdmin gate");
        }
    }

    [Test]
    public void AdminUsersController_is_not_tenant_scoped()
    {
        typeof(AdminUsersController).GetCustomAttribute<TenantScopedAttribute>()
            .Should().BeNull("the platform roster spans every organization");
    }

    [Test]
    public void Every_mutating_route_on_AdminUsersController_is_accounted_for()
    {
        var mutatingActionNames = typeof(AdminUsersController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(action =>
                action.GetCustomAttribute<HttpPutAttribute>() is not null
                || action.GetCustomAttribute<HttpPostAttribute>() is not null
                || action.GetCustomAttribute<HttpPatchAttribute>() is not null
                || action.GetCustomAttribute<HttpDeleteAttribute>() is not null)
            .Select(action => action.Name)
            .ToList();

        mutatingActionNames.Should().BeEquivalentTo(
            new[] { "UpdateUser", "DeleteAvatar", "ChangeRole" },
            "a new mutating route added here would otherwise inherit the controller's looser "
            + "RequirePlatformAdmin gate without anyone noticing");
    }
}
