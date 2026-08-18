using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sellevate.BuildingBlocks.Identity;

namespace Sellevate.BuildingBlocks.Tenancy;

/// <summary>
/// Fills the request's <see cref="TenantContext"/> from the validated identity, and is the only
/// place a request can acquire either of them.
///
/// <para>
/// The organization comes from the <c>X-Organization-Id</c> header, which the gateway sets solely
/// from a validated token after stripping any client-supplied copy. Platform-wide mode comes from
/// the `role` claim of the principal this service authenticated itself — a claim, not a header, so
/// there is no client-writable input anywhere on the path to it. Both facts are covered by tests;
/// the middleware must run after <c>UseAuthentication()</c> or the claim is simply absent and
/// platform staff silently degrade to seeing nothing.
/// </para>
/// </summary>
public sealed class TenantContextMiddleware(RequestDelegate next)
{
    /// <summary>The `role` claim values that read across every organization. Sellevate's own staff
    /// (docs/DECISIONS.md, 2026-08-16) — never an organization's `org_role`.</summary>
    private static readonly string[] PlatformRoles = ["Admin", "SuperAdmin"];

    public async Task InvokeAsync(HttpContext httpContext, TenantContext tenantContext)
    {
        var organizationIdHeaderValue = httpContext.Request.Headers[IdentityHeaders.OrganizationId].ToString();
        var organizationIdWasResolved = Guid.TryParse(organizationIdHeaderValue, out var organizationId);

        if (organizationIdWasResolved)
        {
            tenantContext.SetOrganization(organizationId);
        }

        var callerIsPlatformStaff = IsPlatformStaff(httpContext.User);
        if (callerIsPlatformStaff)
        {
            tenantContext.EnterPlatformMode();
        }

        // Platform staff normally hold no membership and therefore no organization header, so a
        // tenant-scoped route must not turn them away: their scope is every organization, which is
        // a wider answer to "which tenant is this?" rather than a missing one.
        var routeRequiresTenantScope = httpContext.GetEndpoint()?.Metadata.GetMetadata<TenantScopedAttribute>() is not null;
        if (routeRequiresTenantScope && !organizationIdWasResolved && !callerIsPlatformStaff)
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(httpContext);
    }

    /// <summary>
    /// Reads the role off the validated principal only. An impersonation token is minted with
    /// <c>role: User</c> on purpose (Phase 40.9), so impersonating never confers platform-wide
    /// reads — the impersonator sees exactly the one organization they borrowed.
    /// </summary>
    private static bool IsPlatformStaff(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated is not true)
        {
            return false;
        }

        return PlatformRoles.Any(principal.IsInRole);
    }
}
