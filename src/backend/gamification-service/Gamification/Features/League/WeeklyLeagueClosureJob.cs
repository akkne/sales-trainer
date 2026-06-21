using Hangfire;
using Sellevate.Gamification.Features.League.Services.Abstract;

namespace Sellevate.Gamification.Features.League;

/// <summary>
/// [DisableConcurrentExecution] prevents the Hangfire cron from running overlapping
/// instances if a previous execution is still in progress. The rollover itself also
/// re-checks CurrentPeriodEndsAt inside a transaction (optimistic guard) so that a
/// concurrent admin endpoint call cannot create a duplicate next-week league.
/// </summary>
[DisableConcurrentExecution(timeoutInSeconds: 300)]
public sealed class WeeklyLeagueClosureJob(ILeagueService leagueService)
{
    public async Task ExecuteAsync()
    {
        await leagueService.RolloverIfDueAsync();
    }
}
