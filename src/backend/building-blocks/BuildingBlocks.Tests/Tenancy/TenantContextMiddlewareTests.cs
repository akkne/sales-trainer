using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Identity;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.BuildingBlocks.Tests.Tenancy;

[TestFixture]
public class TenantContextMiddlewareTests
{
    private static Endpoint TenantScopedEndpoint()
        => new(requestDelegate: null, metadata: new EndpointMetadataCollection(new TenantScopedAttribute()), displayName: "tenant-scoped-endpoint");

    [Test]
    public async Task InvokeAsync_populates_the_tenant_context_from_a_valid_header_and_calls_next()
    {
        var organizationId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[IdentityHeaders.OrganizationId] = organizationId.ToString();
        var tenantContext = new TenantContext();
        var nextWasInvoked = false;
        var middleware = new TenantContextMiddleware(_ =>
        {
            nextWasInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext, tenantContext);

        tenantContext.OrganizationId.Should().Be(organizationId);
        nextWasInvoked.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Test]
    public async Task InvokeAsync_rejects_a_tenant_scoped_route_with_no_header()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(TenantScopedEndpoint());
        var tenantContext = new TenantContext();
        var nextWasInvoked = false;
        var middleware = new TenantContextMiddleware(_ =>
        {
            nextWasInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext, tenantContext);

        nextWasInvoked.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        tenantContext.OrganizationId.Should().BeNull();
    }

    [Test]
    public async Task InvokeAsync_rejects_a_tenant_scoped_route_with_a_malformed_header()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[IdentityHeaders.OrganizationId] = "not-a-guid";
        httpContext.SetEndpoint(TenantScopedEndpoint());
        var tenantContext = new TenantContext();
        var nextWasInvoked = false;
        var middleware = new TenantContextMiddleware(_ =>
        {
            nextWasInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext, tenantContext);

        nextWasInvoked.Should().BeFalse();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Test]
    public async Task InvokeAsync_allows_a_non_tenant_scoped_route_with_no_header()
    {
        var httpContext = new DefaultHttpContext();
        var tenantContext = new TenantContext();
        var nextWasInvoked = false;
        var middleware = new TenantContextMiddleware(_ =>
        {
            nextWasInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext, tenantContext);

        nextWasInvoked.Should().BeTrue();
        tenantContext.OrganizationId.Should().BeNull();
    }
}
