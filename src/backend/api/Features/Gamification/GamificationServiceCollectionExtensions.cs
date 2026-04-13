namespace SalesTrainer.Api.Features.Gamification;

public static class GamificationServiceCollectionExtensions
{
    public static IServiceCollection AddGamificationFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<StreakResetJob>();
        return services;
    }
}
