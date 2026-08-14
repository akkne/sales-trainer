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

    private void Enforce(DbContext? context)
    {
        if (context is null || tenantContext.IsSystem)
        {
            return;
        }

        var currentOrganizationId = tenantContext.OrganizationId
            ?? throw new InvalidOperationException("Organization context is not set.");

        foreach (var entry in context.ChangeTracker.Entries<ITenantScoped>())
        {
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
