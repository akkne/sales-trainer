using Hangfire;
using Microsoft.EntityFrameworkCore;
using Sellevate.Gamification.Features.League.Services.Abstract;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Features.League;

/// <summary>
/// [DisableConcurrentExecution] prevents the Hangfire cron from running overlapping
/// instances if a previous execution is still in progress. The rollover itself also
/// re-checks CurrentPeriodEndsAt inside a transaction (optimistic guard) so that a
/// concurrent admin endpoint call cannot create a duplicate next-week league.
///
/// <para>
/// Phase 40.13 made this an <b>iterate-organizations</b> job (docs/TENANCY/TENANCY.md §1.6). Each
/// organization now runs its own league week: <c>LeagueSettings</c> became tenant-scoped, because
/// <c>CurrentPeriodStartDate</c>/<c>CurrentPeriodEndsAt</c> are the state of a running competition
/// rather than configuration, and a single shared row meant the first organization to roll over
/// advanced the period for everybody — after which every other organization's rollover found the
/// period already advanced, bailed out, and left its leagues open forever.
/// </para>
///
/// <para>
/// The organizations come from <c>LeagueSettings</c> rather than from <c>Leagues</c>: an
/// organization has a period to roll over as soon as it has settings, even in the week when nobody
/// earned enough to be placed in a league. Reading from <c>Leagues</c> would skip exactly those
/// quiet weeks and leave the period frozen.
/// </para>
/// </summary>
[DisableConcurrentExecution(timeoutInSeconds: 300)]
public sealed class WeeklyLeagueClosureJob(
    IServiceScopeFactory scopeFactory,
    ILogger<WeeklyLeagueClosureJob> logger)
{
    public async Task ExecuteAsync()
    {
        var organizationIds = await TenantJobScope.EnumerateOrganizationsAsync(
            scopeFactory,
            databaseContext => databaseContext.LeagueSettings
                .IgnoreQueryFilters()
                .Select(settings => settings.OrganizationId),
            CancellationToken.None);

        if (organizationIds.Count == 0)
        {
            logger.LogInformation("WeeklyLeagueClosureJob: no organization has a league period yet.");
            return;
        }

        foreach (var organizationId in organizationIds)
        {
            using var scope = TenantJobScope.ForOrganization(scopeFactory, organizationId);

            try
            {
                var leagueService = scope.ServiceProvider.GetRequiredService<ILeagueService>();
                await leagueService.RolloverIfDueAsync();
            }
            catch (Exception exception)
            {
                // One organization's rollover failing must not freeze every other organization's
                // league week until next Monday.
                logger.LogError(
                    exception,
                    "WeeklyLeagueClosureJob failed for organization {OrganizationId}; other organizations continue",
                    organizationId);
            }
        }

        logger.LogInformation(
            "WeeklyLeagueClosureJob: checked {OrganizationCount} organization(s) for a due rollover.",
            organizationIds.Count);
    }
}
