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
}
