using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Gamification.Infrastructure.Data;

/// <summary>
/// Phase 40.13. The shared half of gamification-service's two Hangfire jobs: how they find the
/// organizations to work through, and how they get a scope pointed at one.
///
/// <para>
/// docs/TENANCY/TENANCY.md §1.6 allows a background job exactly two shapes — "iterate
/// organizations" or "system", declared explicitly — and every job in this service that touches
/// user rows is the first shape. That is not a style preference: the alternative, running the job
/// once with no tenant, means the query filters compare against <c>null</c> and the RLS policies
/// return nothing, so a streak reset would report success having reset zero streaks.
/// </para>
///
/// <para>
/// The organization list comes from gamification-db's own tables rather than from a replicated
/// tenant registry, matching company-service's 40.12 decision: the question a job asks is "which
/// organizations have something for me to do", which is a fact of this database. A registry-driven
/// loop would iterate organizations with no gamification data at all and — the part that matters —
/// would silently skip one whose registry row had not replicated yet, turning replication lag into
/// a skipped job.
/// </para>
///
/// <para>
/// <b>Operational dependency:</b> the enumeration runs in system mode, which issues no
/// <c>SET LOCAL app.organization_id</c>, so it returns rows only for a role that bypasses RLS. That
/// holds today (the service connects as the owning superuser) and is recorded in
/// docs/DONT_FORGET.md as a prerequisite for rolling out the <c>sellevate_app</c> role: a
/// <c>NOBYPASSRLS</c> role would make this return an empty list and both jobs would go quiet
/// without erroring.
/// </para>
/// </summary>
internal static class TenantJobScope
{
    /// <summary>
    /// The distinct organizations that own a row in <paramref name="selectOrganizationIds"/>.
    /// Selects a single column — organization ids — and never reads row content.
    /// </summary>
    internal static async Task<IReadOnlyList<Guid>> EnumerateOrganizationsAsync(
        IServiceScopeFactory scopeFactory,
        Func<GamificationDbContext, IQueryable<Guid>> selectOrganizationIds,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().EnterSystemMode();

        var databaseContext = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();

        return await selectOrganizationIds(databaseContext)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// A fresh scope with <paramref name="organizationId"/> set on it. One scope per organization,
    /// never one reused across them: <see cref="TenantContext"/> refuses to be re-pointed at a
    /// second organization, which is the guard that turns "the loop forgot to reset the tenant"
    /// from a silent cross-tenant write into an exception.
    /// </summary>
    internal static IServiceScope ForOrganization(IServiceScopeFactory scopeFactory, Guid organizationId)
    {
        var scope = scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetOrganization(organizationId);
        return scope;
    }
}
