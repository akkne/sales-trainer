using FluentAssertions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.BuildingBlocks.Tests.Tenancy;

[TestFixture]
public class TenantConnectionInterceptorTests
{
    [Test]
    public void BuildSetLocalCommandText_with_an_organization_set_emits_a_set_local_statement()
    {
        var organizationId = Guid.NewGuid();
        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(organizationId);
        var interceptor = new TenantConnectionInterceptor(tenantContext);

        var commandText = interceptor.BuildSetLocalCommandText();

        commandText.Should().Be($"SET LOCAL app.organization_id = '{organizationId:D}'");
    }

    [Test]
    public void BuildSetLocalCommandText_never_uses_a_bare_set()
    {
        var organizationId = Guid.NewGuid();

        var commandText = TenantConnectionInterceptor.BuildSetLocalCommandText(organizationId);

        commandText.Should().StartWith("SET LOCAL ").And.NotContain("SET app.");
    }

    [Test]
    public void BuildSetLocalCommandText_with_no_organization_set_returns_null()
    {
        var tenantContext = new TenantContext();
        var interceptor = new TenantConnectionInterceptor(tenantContext);

        var commandText = interceptor.BuildSetLocalCommandText();

        commandText.Should().BeNull();
    }

    [Test]
    public void BuildSetLocalCommandText_in_system_mode_returns_null_because_system_connections_bypass_rls_via_role()
    {
        var tenantContext = new TenantContext();
        tenantContext.EnterSystemMode();
        var interceptor = new TenantConnectionInterceptor(tenantContext);

        var commandText = interceptor.BuildSetLocalCommandText();

        commandText.Should().BeNull();
    }

    [Test]
    public void BuildSetLocalCommandText_in_platform_mode_turns_the_platform_guc_on()
    {
        var tenantContext = new TenantContext();
        tenantContext.EnterPlatformMode();
        var interceptor = new TenantConnectionInterceptor(tenantContext);

        var commandText = interceptor.BuildSetLocalCommandText();

        commandText.Should().Be("SET LOCAL app.platform_mode = 'on'");
    }

    /// <summary>
    /// A platform administrator who also belongs to an organization gets both: the organization is
    /// what their writes are checked against, platform mode is what widens their reads.
    /// </summary>
    [Test]
    public void BuildSetLocalCommandText_emits_both_settings_for_platform_staff_with_an_organization()
    {
        var organizationId = Guid.NewGuid();
        var tenantContext = new TenantContext();
        tenantContext.SetOrganization(organizationId);
        tenantContext.EnterPlatformMode();
        var interceptor = new TenantConnectionInterceptor(tenantContext);

        var commandText = interceptor.BuildSetLocalCommandText();

        commandText.Should().Be(
            $"SET LOCAL app.organization_id = '{organizationId:D}'; SET LOCAL app.platform_mode = 'on'");
    }

    [Test]
    public void BuildPlatformModeSetLocalCommandText_never_uses_a_bare_set()
    {
        var commandText = TenantConnectionInterceptor.BuildPlatformModeSetLocalCommandText();

        commandText.Should().StartWith("SET LOCAL ").And.NotContain("SET app.");
    }

    /// <summary>
    /// System mode wins over everything, including a scope that somehow reached platform mode. A
    /// background job runs under a role, not under a human's entitlement.
    /// </summary>
    [Test]
    public void BuildSetLocalCommandText_in_system_mode_emits_nothing_even_if_platform_mode_was_requested()
    {
        var tenantContext = new TenantContext();
        tenantContext.EnterSystemMode();
        var interceptor = new TenantConnectionInterceptor(tenantContext);

        var enteringPlatformMode = () => tenantContext.EnterPlatformMode();

        enteringPlatformMode.Should().Throw<InvalidOperationException>();
        interceptor.BuildSetLocalCommandText().Should().BeNull();
    }
}
