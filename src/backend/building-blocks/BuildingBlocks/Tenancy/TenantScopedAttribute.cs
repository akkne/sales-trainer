namespace Sellevate.BuildingBlocks.Tenancy;

/// <summary>
/// Endpoint metadata declaring that a route may only be served for a caller with a resolved tenant.
/// <see cref="TenantContextMiddleware"/> answers it with 403 when neither an organization nor
/// platform-wide staff mode could be established.
///
/// <para>
/// Apply it to minimal-API endpoints through
/// <see cref="TenantScopedEndpointConventionBuilderExtensions.RequireTenantScope{TBuilder}"/> rather
/// than by hand, so the metadata and the middleware stay a matched pair.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class TenantScopedAttribute : Attribute
{
}
