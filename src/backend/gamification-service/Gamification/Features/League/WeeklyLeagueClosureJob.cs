using Sellevate.Gamification.Features.League.Services.Abstract;

namespace Sellevate.Gamification.Features.League;

public sealed class WeeklyLeagueClosureJob(ILeagueService leagueService)
{
    public async Task ExecuteAsync()
    {
        await leagueService.RolloverIfDueAsync();
    }
}
