using Microsoft.AspNetCore.Builder;

namespace Sellevate.BuildingBlocks.Tenancy;

public static class TenantContextApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSellevateTenantContext(this IApplicationBuilder applicationBuilder)
        => applicationBuilder.UseMiddleware<TenantContextMiddleware>();
}
