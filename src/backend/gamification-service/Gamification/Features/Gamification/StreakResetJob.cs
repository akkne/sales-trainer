using Microsoft.EntityFrameworkCore;
using Sellevate.Gamification.Features.Gamification.Services.Abstract;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Features.Gamification;

/// <summary>
/// Daily Hangfire job that zeroes streaks nobody kept alive.
///
/// <para>
/// Phase 40.13 made this an <b>iterate-organizations</b> job (docs/TENANCY/TENANCY.md §1.6), which
/// is the correct mode for anything that writes user rows. It is not merely tidier than the old
/// single cross-tenant scan — it is the only mode that still works: with query filters and RLS in
/// place, one pass with no tenant compares <c>OrganizationId</c> against <c>null</c>, matches
/// nothing, and logs "no stale streaks found" every night while every streak in the product
/// silently survives. A job that fails by doing nothing and saying so cheerfully is the failure
/// mode 40.14 exists to hunt, so the enumeration is explicit and every write happens under a
/// concrete organization.
/// </para>
///
/// <para>
/// One organization's failure is logged and swallowed on purpose: it must not stop every other
/// organization's reset for the rest of the night, which is the whole reason the loop exists. The
/// exception is never silently dropped — it is logged with the organization it belongs to, and the
/// run's summary line reports how many organizations were visited.
/// </para>
/// </summary>
public sealed class StreakResetJob(
    IServiceScopeFactory scopeFactory,
    ILogger<StreakResetJob> logger)
{
    public async Task ExecuteAsync()
    {
        var organizationIds = await TenantJobScope.EnumerateOrganizationsAsync(
            scopeFactory,
            databaseContext => databaseContext.UserStreaks
                .IgnoreQueryFilters()
                .Where(streak => streak.CurrentStreakDayCount > 0)
                .Select(streak => streak.OrganizationId),
            CancellationToken.None);

        if (organizationIds.Count == 0)
        {
            logger.LogInformation("StreakResetJob: no organization has a live streak.");
            return;
        }

        var totalResetCount = 0;
        foreach (var organizationId in organizationIds)
        {
            using var scope = TenantJobScope.ForOrganization(scopeFactory, organizationId);

            try
            {
                var databaseContext = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
                var streakClock = scope.ServiceProvider.GetRequiredService<IStreakClock>();

                totalResetCount += await ResetStaleStreaksAsync(databaseContext, streakClock, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "StreakResetJob failed for organization {OrganizationId}; other organizations continue",
                    organizationId);
            }
        }

        logger.LogInformation(
            "StreakResetJob: reset {Count} stale streak(s) across {OrganizationCount} organization(s).",
            totalResetCount, organizationIds.Count);
    }

    /// <summary>
    /// The reset itself, for one organization. Takes the already-scoped context rather than reading
    /// a tenant of its own: everything it touches is filtered by the caller's scope, so there is no
    /// organization predicate written out here and no way to write one that disagrees with the
    /// scope. Internal so the unit tests can exercise the rule without a Hangfire server.
    /// </summary>
    internal static async Task<int> ResetStaleStreaksAsync(
        GamificationDbContext databaseContext,
        IStreakClock streakClock,
        CancellationToken cancellationToken)
    {
        var yesterday = streakClock.Today().AddDays(-1);

        var staleStreaks = await databaseContext.UserStreaks
            .Where(streak => streak.CurrentStreakDayCount > 0
                        && (streak.LastActivityDate == null || streak.LastActivityDate < yesterday))
            .ToListAsync(cancellationToken);

        if (staleStreaks.Count == 0)
        {
            return 0;
        }

        foreach (var streak in staleStreaks)
        {
            streak.CurrentStreakDayCount = 0;
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
        return staleStreaks.Count;
    }
}
