namespace SalesTrainer.Api.Features.League;

public class WeeklyLeagueClosureJob(ILeagueService leagueService)
{
    public async Task ExecuteAsync()
    {
        await leagueService.CloseCurrentLeagueAndCreateNextAsync();
    }
}
