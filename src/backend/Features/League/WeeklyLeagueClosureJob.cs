namespace SalesTrainer.Api.Features.League;

public class WeeklyLeagueClosureJob(LeagueService leagueService)
{
    public async Task ExecuteAsync()
    {
        await leagueService.CloseCurrentLeagueAndCreateNextAsync();
    }
}
