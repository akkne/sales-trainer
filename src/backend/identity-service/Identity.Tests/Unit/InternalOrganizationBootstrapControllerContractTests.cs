using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Common.Security;
using Sellevate.Identity.Features.Organizations.Endpoints;

namespace Sellevate.Identity.Tests.Unit;

/// <summary>
/// <c>internal/organizations/{organizationId}/bootstrap-admin</c> is reachable by any caller holding
/// the shared secret, with no JWT and no tenant of its own — the attributes below are its entire
/// protection, and each fails silently rather than loudly if it is ever removed, which is why they
/// are pinned here rather than left to be noticed at the next incident. Style follows identity's own
/// <c>PlatformAdminControllerContractTests</c> and <c>InternalMembershipsControllerTests</c>.
/// </summary>
[TestFixture]
public sealed class InternalOrganizationBootstrapControllerContractTests
{
    [Test]
    public void The_route_stays_under_the_internal_prefix()
    {
        typeof(InternalOrganizationBootstrapController).GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("internal/organizations/{organizationId:guid}/bootstrap-admin",
                "the internal/ prefix is what keeps this route out of the gateway's routing table");
    }

    [Test]
    public void The_route_is_guarded_by_the_internal_service_secret()
    {
        typeof(InternalOrganizationBootstrapController).GetCustomAttributes<ServiceFilterAttribute>()
            .Select(attribute => attribute.ServiceType)
            .Should().Contain(typeof(InternalServiceAuthFilter),
                "without this filter anybody able to address the pod could bootstrap an administrator "
                + "for any organization");
    }

    [Test]
    public void The_controller_carries_no_Authorize_attribute()
    {
        typeof(InternalOrganizationBootstrapController).GetCustomAttribute<AuthorizeAttribute>()
            .Should().BeNull(
                "the caller is organization-service itself and carries no JWT — an [Authorize] gate "
                + "here would be unreachable by design, not an extra safeguard");
    }

    [Test]
    public void The_controller_is_not_tenant_scoped()
    {
        typeof(InternalOrganizationBootstrapController).GetCustomAttribute<TenantScopedAttribute>()
            .Should().BeNull(
                "the organization is named in the route because the caller has no membership in it "
                + "to carry an X-Organization-Id header for — the same TENANCY.md §1.3 carve-out "
                + "PlatformAdminController already relies on");
    }
}
