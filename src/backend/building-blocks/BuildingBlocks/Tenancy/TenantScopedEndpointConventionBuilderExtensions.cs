using Microsoft.AspNetCore.Builder;

namespace Sellevate.BuildingBlocks.Tenancy;

public static class TenantScopedEndpointConventionBuilderExtensions
{
    public static TBuilder RequireTenantScope<TBuilder>(this TBuilder endpointConventionBuilder)
        where TBuilder : IEndpointConventionBuilder
    {
        endpointConventionBuilder.WithMetadata(new TenantScopedAttribute());
        return endpointConventionBuilder;
    }
}
