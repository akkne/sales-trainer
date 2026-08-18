using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Sellevate.BuildingBlocks.Tenancy;

/// <summary>
/// The write-side half of tenant isolation (docs/TENANCY/TENANCY.md §2), and the only layer that acts
/// on a save. The EF query filter is read-side ergonomics and does nothing here; Postgres RLS is the
/// real boundary but reports a violation as zero affected rows, which is a silent no-op rather than a
/// diagnosis. This interceptor is what turns a cross-tenant write into a named exception at the
/// moment the offending code runs.
///
/// <para>
/// It stamps the scope's organization onto a new <see cref="ITenantScoped"/> row that has none, and
/// refuses any entry — added, modified or deleted — that names a different one. It is a guard, not a
/// filter: a service must still add it to its <c>DbContext</c> via <c>AddInterceptors</c>, and a
/// context registered with EF Core's pooled helper would defeat the whole model (see
/// docs/CODESTYLE.md §6).
/// </para>
/// </summary>
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
        if (context is null)
        {
            return;
        }

        if (tenantContext.IsSystem)
        {
            EnforceSystemMode(context);
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

    /// <summary>
    /// System mode has no ambient organization to stamp, so it cannot run the checks above — that is
    /// the whole reason it exists. What it must still refuse is the one thing an unset tenant can
    /// silently produce: a brand-new tenant-scoped row carrying the default <c>Guid.Empty</c>, which
    /// belongs to no organization, is visible to none of them, and is unattributable forever after.
    ///
    /// <para>
    /// Added by the 40.14 audit as a backstop, not because any current path hits it: every
    /// system-mode writer today touches platform-global tables only (the outbox relay writes
    /// <c>OutboxMessage</c>, the replica consumers write user/organization projections, the two
    /// identity cleanups delete non-tenant rows). The backstop exists because the cheapest way to
    /// silence a consumer that throws "carries no organization" is to flip its
    /// <c>RequiresOrganization</c> to <c>false</c> — which fixes the exception by moving the handler
    /// into system mode and turning a loud failure into zero-organization data. This makes that
    /// shortcut fail loudly too.
    /// </para>
    ///
    /// <para>
    /// A system-mode writer with a genuine organization in hand is unaffected: it sets
    /// <c>OrganizationId</c> on the entity explicitly, which is exactly the auditable act being
    /// asked for.
    /// </para>
    /// </summary>
    private static void EnforceSystemMode(DbContext context)
    {
        foreach (var entry in context.ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State == EntityState.Added && entry.Entity.OrganizationId == Guid.Empty)
            {
                throw new InvalidOperationException(
                    $"System mode may not create {entry.Metadata.Name} without an explicit organization.");
            }
        }
    }
}
