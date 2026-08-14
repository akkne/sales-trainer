using Microsoft.AspNetCore.Http;
using Sellevate.BuildingBlocks.Identity;

namespace Sellevate.BuildingBlocks.Tenancy;

public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, TenantContext tenantContext)
    {
        var organizationIdHeaderValue = httpContext.Request.Headers[IdentityHeaders.OrganizationId].ToString();
        var organizationIdWasResolved = Guid.TryParse(organizationIdHeaderValue, out var organizationId);

        if (organizationIdWasResolved)
        {
            tenantContext.SetOrganization(organizationId);
        }

        var routeRequiresTenantScope = httpContext.GetEndpoint()?.Metadata.GetMetadata<TenantScopedAttribute>() is not null;
        if (routeRequiresTenantScope && !organizationIdWasResolved)
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next(httpContext);
    }
}
