using System.Security.Claims;
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

    /// <summary>
    /// An authenticated principal carrying one role claim, shaped the way the JWT handler shapes it
    /// (<see cref="ClaimTypes.Role"/>), so <c>IsInRole</c> answers the way it does in a real request.
    /// </summary>
    private static ClaimsPrincipal PrincipalInRole(string role)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], authenticationType: "TestJwt"));

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

    [TestCase("Admin")]
    [TestCase("SuperAdmin")]
    public async Task InvokeAsync_enters_platform_mode_for_platform_staff(string platformRole)
    {
        var httpContext = new DefaultHttpContext { User = PrincipalInRole(platformRole) };
        var tenantContext = new TenantContext();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(httpContext, tenantContext);

        tenantContext.IsPlatformWide.Should().BeTrue();
        tenantContext.IsSystem.Should().BeFalse();
    }

    /// <summary>
    /// Platform staff normally belong to no organization, so a tenant-scoped route must let them
    /// through rather than 403 on the missing header. Their scope is every organization — a wider
    /// answer to "which tenant?", not a missing one.
    /// </summary>
    [Test]
    public async Task InvokeAsync_allows_platform_staff_onto_a_tenant_scoped_route_with_no_header()
    {
        var httpContext = new DefaultHttpContext { User = PrincipalInRole("SuperAdmin") };
        httpContext.SetEndpoint(TenantScopedEndpoint());
        var tenantContext = new TenantContext();
        var nextWasInvoked = false;
        var middleware = new TenantContextMiddleware(_ =>
        {
            nextWasInvoked = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext, tenantContext);

        nextWasInvoked.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        tenantContext.IsPlatformWide.Should().BeTrue();
        tenantContext.OrganizationId.Should().BeNull();
    }

    /// <summary>
    /// A platform administrator who also belongs to an organization keeps it: the widening decides
    /// what they can see, the organization decides where their writes land.
    /// </summary>
    [Test]
    public async Task InvokeAsync_keeps_the_organization_of_platform_staff_who_have_one()
    {
        var organizationId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext { User = PrincipalInRole("Admin") };
        httpContext.Request.Headers[IdentityHeaders.OrganizationId] = organizationId.ToString();
        var tenantContext = new TenantContext();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(httpContext, tenantContext);

        tenantContext.IsPlatformWide.Should().BeTrue();
        tenantContext.OrganizationId.Should().Be(organizationId);
    }

    /// <summary>
    /// The whole point of reading the role off the validated principal: nothing a client can write
    /// reaches this decision. An impersonation token is minted with <c>role: User</c> (40.9), which
    /// is why the second case matters as much as the first.
    /// </summary>
    [TestCase("User")]
    [TestCase("TenancyAdmin")]
    [TestCase("TenancySuperAdmin")]
    public async Task InvokeAsync_does_not_enter_platform_mode_for_anybody_else(string role)
    {
        var httpContext = new DefaultHttpContext { User = PrincipalInRole(role) };
        var tenantContext = new TenantContext();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(httpContext, tenantContext);

        tenantContext.IsPlatformWide.Should().BeFalse();
    }

    [Test]
    public async Task InvokeAsync_ignores_platform_roles_on_an_unauthenticated_principal()
    {
        var httpContext = new DefaultHttpContext
        {
            // No authentication type: exactly what an unauthenticated request looks like, and what
            // a forged header would have to masquerade as.
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "SuperAdmin")])),
        };
        var tenantContext = new TenantContext();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(httpContext, tenantContext);

        tenantContext.IsPlatformWide.Should().BeFalse();
    }

    /// <summary>
    /// A header cannot buy platform mode. The organization header is the only tenancy input a
    /// request carries, and it names an organization — it never grants a privilege.
    /// </summary>
    [Test]
    public async Task InvokeAsync_does_not_enter_platform_mode_from_headers_alone()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[IdentityHeaders.UserRole] = "SuperAdmin";
        httpContext.Request.Headers["X-Platform-Mode"] = "on";
        httpContext.Request.Headers[IdentityHeaders.OrganizationId] = Guid.NewGuid().ToString();
        var tenantContext = new TenantContext();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(httpContext, tenantContext);

        tenantContext.IsPlatformWide.Should().BeFalse();
    }
}
