using Microsoft.AspNetCore.Builder;

namespace Sellevate.BuildingBlocks.Tenancy;

/// <summary>
/// Endpoint-building sugar for attaching <see cref="TenantScopedAttribute"/> metadata.
/// </summary>
public static class TenantScopedEndpointConventionBuilderExtensions
{
    /// <summary>
    /// Declares that the endpoint may only be served for a caller with a resolved tenant. Enforcement
    /// lives in <see cref="TenantContextMiddleware"/>, so this call has no effect in a pipeline that
    /// does not run <see cref="TenantContextApplicationBuilderExtensions.UseSellevateTenantContext"/>.
    /// </summary>
    public static TBuilder RequireTenantScope<TBuilder>(this TBuilder endpointConventionBuilder)
        where TBuilder : IEndpointConventionBuilder
    {
        endpointConventionBuilder.WithMetadata(new TenantScopedAttribute());
        return endpointConventionBuilder;
    }
}
