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

    [Test]
    public void EnterPlatformMode_sets_IsPlatformWide_and_is_not_system_mode()
    {
        var tenantContext = new TenantContext();

        tenantContext.EnterPlatformMode();

        tenantContext.IsPlatformWide.Should().BeTrue();
        tenantContext.IsSystem.Should().BeFalse();
        tenantContext.OrganizationId.Should().BeNull();
    }

    /// <summary>
    /// The two coexist on purpose: a Sellevate administrator who also belongs to an organization
    /// reads across all of them and writes into theirs. Order must not matter — the middleware sets
    /// the organization first, but nothing should depend on that.
    /// </summary>
    [Test]
    public void EnterPlatformMode_and_SetOrganization_coexist_in_either_order()
    {
        var organizationId = Guid.NewGuid();

        var organizationFirst = new TenantContext();
        organizationFirst.SetOrganization(organizationId);
        organizationFirst.EnterPlatformMode();

        var platformFirst = new TenantContext();
        platformFirst.EnterPlatformMode();
        platformFirst.SetOrganization(organizationId);

        organizationFirst.IsPlatformWide.Should().BeTrue();
        organizationFirst.OrganizationId.Should().Be(organizationId);
        platformFirst.IsPlatformWide.Should().BeTrue();
        platformFirst.OrganizationId.Should().Be(organizationId);
    }

    /// <summary>
    /// System mode and platform-wide mode are not two names for "sees everything". One is a
    /// background job with nobody behind it, the other is a person entitled to look. A scope that
    /// claimed both would be a job inheriting a human's privileges, or the reverse.
    /// </summary>
    [Test]
    public void EnterPlatformMode_rejects_a_context_already_in_system_mode()
    {
        var tenantContext = new TenantContext();
        tenantContext.EnterSystemMode();

        var act = tenantContext.EnterPlatformMode;

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void EnterSystemMode_rejects_a_context_already_in_platform_mode()
    {
        var tenantContext = new TenantContext();
        tenantContext.EnterPlatformMode();

        var act = tenantContext.EnterSystemMode;

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void EnterPlatformMode_is_idempotent()
    {
        var tenantContext = new TenantContext();

        tenantContext.EnterPlatformMode();
        tenantContext.EnterPlatformMode();

        tenantContext.IsPlatformWide.Should().BeTrue();
    }

    [Test]
    public void A_fresh_context_is_neither_platform_wide_nor_system()
    {
        var tenantContext = new TenantContext();

        tenantContext.IsPlatformWide.Should().BeFalse();
        tenantContext.IsSystem.Should().BeFalse();
        tenantContext.OrganizationId.Should().BeNull();
    }
}
