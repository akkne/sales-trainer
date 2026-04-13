namespace SalesTrainer.Api.Features.League;

public static class LeagueServiceCollectionExtensions
{
    public static IServiceCollection AddLeagueFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<ILeagueService, LeagueService>();
        services.AddScoped<WeeklyLeagueClosureJob>();
        return services;
    }
}
