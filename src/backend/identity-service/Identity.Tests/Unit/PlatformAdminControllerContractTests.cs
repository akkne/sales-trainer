using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Features.Invites.Endpoints;
using Sellevate.Identity.Features.PlatformAdmin.Endpoints;

namespace Sellevate.Identity.Tests.Unit;

/// <summary>
/// Encodes the Phase 40.9 access decisions as executable checks. Both are the kind of property
/// that is invisible in a diff once someone edits an attribute: a platform route silently losing
/// its <c>RequireSuperAdmin</c> gate, or gaining a <c>[TenantScoped]</c> one it must not have.
/// </summary>
[TestFixture]
public class PlatformAdminControllerContractTests
{
    [Test]
    public void PlatformAdminController_is_gated_by_RequireSuperAdmin()
    {
        var authorizeAttribute = typeof(PlatformAdminController)
            .GetCustomAttribute<AuthorizeAttribute>();

        authorizeAttribute.Should().NotBeNull();
        authorizeAttribute!.Policy.Should().Be("RequireSuperAdmin");
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
    public void InvitesController_remains_tenant_scoped_and_org_admin_gated()
    {
        typeof(InvitesController).GetCustomAttribute<TenantScopedAttribute>().Should().NotBeNull();
        typeof(InvitesController).GetCustomAttribute<AuthorizeAttribute>()!
            .Policy.Should().Be("RequireOrgAdmin",
                "the platform bootstrap path must not have loosened the ordinary invite route");
    }
}
