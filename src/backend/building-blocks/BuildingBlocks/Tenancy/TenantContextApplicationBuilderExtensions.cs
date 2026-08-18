using Microsoft.AspNetCore.Builder;

namespace Sellevate.BuildingBlocks.Tenancy;

/// <summary>
/// Pipeline registration for <see cref="TenantContextMiddleware"/>.
/// </summary>
public static class TenantContextApplicationBuilderExtensions
{
    /// <summary>
    /// Inserts the tenant-context middleware at the current position in the pipeline. Every service
    /// calls it <b>after</b> <c>UseAuthentication()</c> and <c>UseAuthorization()</c>, and that order
    /// is load-bearing on both sides: before authentication the `role` claim is absent and platform
    /// staff silently degrade to seeing nothing, and before routing has selected the endpoint its
    /// <see cref="TenantScopedAttribute"/> metadata is invisible, so a tenant-scoped route runs with
    /// no tenant instead of returning 403.
    /// </summary>
    public static IApplicationBuilder UseSellevateTenantContext(this IApplicationBuilder applicationBuilder)
        => applicationBuilder.UseMiddleware<TenantContextMiddleware>();
}
