using FluentAssertions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;
using OrganizationEntity = Sellevate.Organization.Features.Organizations.Models.Organization;
using OrganizationProfileEntity = Sellevate.Organization.Features.Organizations.Models.OrganizationProfile;

namespace Sellevate.Organization.Tests.Unit;

/// <summary>
/// Encodes the scope decision from docs/DECISIONS.md as an executable check: the tenant
/// registry row is never itself tenant-scoped (it IS the registry — see
/// docs/TENANCY/TENANCY.md §1.2), while the per-organization profile is (it belongs to exactly
/// one organization and is protected by the Stage A write guard + RLS). A future edit that
/// accidentally makes <see cref="OrganizationEntity"/> implement <see cref="ITenantScoped"/>
/// would apply <c>EnableTenantRls</c>-style isolation to the very table that has to remain
/// readable across organizations for the platform admin panel — this test catches that.
/// </summary>
[TestFixture]
public sealed class OrganizationTenancyScopeTests
{
    [Test]
    public void Organization_registry_entity_is_not_tenant_scoped()
    {
        typeof(OrganizationEntity).Should().NotBeAssignableTo<ITenantScoped>();
    }

    [Test]
    public void OrganizationProfile_entity_is_tenant_scoped()
    {
        typeof(OrganizationProfileEntity).Should().BeAssignableTo<ITenantScoped>();
    }
}
