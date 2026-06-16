using SalesTrainer.Api.Features.League.Services.Abstract;

namespace SalesTrainer.Api.Features.League;

public sealed class WeeklyLeagueClosureJob(ILeagueService leagueService)
{
    public async Task ExecuteAsync()
    {
        // Runs frequently and closes the current period only once its configured
        // end moment has passed, so an admin-set end date drives the schedule.
        await leagueService.RolloverIfDueAsync();
    }
}
