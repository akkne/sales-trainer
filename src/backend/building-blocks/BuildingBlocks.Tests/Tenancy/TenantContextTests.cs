using FluentAssertions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.BuildingBlocks.Tests.Tenancy;

[TestFixture]
public class TenantContextTests
{
    [Test]
    public void SetOrganization_populates_OrganizationId()
    {
        var organizationId = Guid.NewGuid();
        var tenantContext = new TenantContext();

        tenantContext.SetOrganization(organizationId);

        tenantContext.OrganizationId.Should().Be(organizationId);
        tenantContext.IsSystem.Should().BeFalse();
    }

    [Test]
    public void SetOrganization_rejects_an_empty_guid()
    {
        var tenantContext = new TenantContext();

        var act = () => tenantContext.SetOrganization(Guid.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void SetOrganization_rejects_switching_to_a_different_organization_within_the_same_scope()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(Guid.NewGuid());

        var act = () => tenantContext.SetOrganization(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void EnterSystemMode_sets_IsSystem_and_leaves_OrganizationId_unset()
    {
        var tenantContext = new TenantContext();

        tenantContext.EnterSystemMode();

        tenantContext.IsSystem.Should().BeTrue();
        tenantContext.OrganizationId.Should().BeNull();
    }

    [Test]
    public void EnterSystemMode_rejects_a_context_that_already_has_an_organization()
    {
        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(Guid.NewGuid());

        var act = tenantContext.EnterSystemMode;

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void SetOrganization_rejects_a_context_already_in_system_mode()
    {
        var tenantContext = new TenantContext();
        tenantContext.EnterSystemMode();

        var act = () => tenantContext.SetOrganization(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }
}
