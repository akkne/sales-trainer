using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Sellevate.BuildingBlocks.Tenancy;

public sealed class TenantSaveChangesInterceptor(ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Enforce(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Enforce(eventData.Context);
        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// The missing-organization guard is evaluated per <see cref="ITenantScoped"/> entry, not once
    /// up front: a service whose database holds both tenant-scoped and platform-global tables
    /// (identity-service — <c>Invites</c> alongside <c>Users</c>/<c>RefreshTokens</c>) must still be
    /// able to write the global ones on an unauthenticated request that carries no
    /// <c>X-Organization-Id</c> at all, such as login or token refresh. A save that touches no
    /// tenant-scoped entity is therefore not a tenancy event and needs no organization.
    /// See docs/DECISIONS.md (2026-08-15, "Tenant write guard is per-entry").
    /// </summary>
    private void Enforce(DbContext? context)
    {
        if (context is null || tenantContext.IsSystem)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ITenantScoped>())
        {
            var currentOrganizationId = tenantContext.OrganizationId
                ?? throw new InvalidOperationException("Organization context is not set.");

            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.OrganizationId == Guid.Empty)
                    {
                        entry.Entity.OrganizationId = currentOrganizationId;
                    }
                    else if (entry.Entity.OrganizationId != currentOrganizationId)
                    {
                        throw new CrossTenantWriteException(entry.Metadata.Name, currentOrganizationId);
                    }

                    break;

                case EntityState.Modified:
                case EntityState.Deleted:
                    var originalOrganizationId = entry.OriginalValues
                        .GetValue<Guid>(nameof(ITenantScoped.OrganizationId));

                    if (originalOrganizationId != currentOrganizationId
                        || entry.Entity.OrganizationId != originalOrganizationId)
                    {
                        throw new CrossTenantWriteException(entry.Metadata.Name, currentOrganizationId);
                    }

                    break;
            }
        }
    }
}
