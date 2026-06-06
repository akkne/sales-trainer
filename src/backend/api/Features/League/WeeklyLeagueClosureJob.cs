using SalesTrainer.Api.Features.League.Services.Abstract;

namespace SalesTrainer.Api.Features.League;

public sealed class WeeklyLeagueClosureJob(ILeagueService leagueService)
{
    public async Task ExecuteAsync()
    {
        await leagueService.CloseCurrentLeagueAndCreateNextAsync();
    }
}
