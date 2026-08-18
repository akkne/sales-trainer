namespace Sellevate.BuildingBlocks.Tenancy;

/// <summary>
/// Marks an entity whose every row belongs to exactly one organization, which is what makes it
/// visible to <see cref="TenantSaveChangesInterceptor"/>'s per-entry write guard.
///
/// <para>
/// The setter is required, not an oversight: on an <c>Added</c> entry the interceptor stamps the
/// scope's organization when the value is still <see cref="Guid.Empty"/>, so a caller does not have
/// to thread the tenant through every construction site. Implement this only for
/// "Tenant data" tables in the sense of docs/TENANCY/TENANCY.md §1.2 — a content table whose
/// <c>null</c> means "global" cannot use a non-nullable <see cref="Guid"/> and is filtered by its
/// own RLS policy instead.
/// </para>
/// </summary>
public interface ITenantScoped
{
    Guid OrganizationId { get; set; }
}
