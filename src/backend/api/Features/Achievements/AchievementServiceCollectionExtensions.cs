namespace SalesTrainer.Api.Features.Achievements;

public static class AchievementServiceCollectionExtensions
{
    public static IServiceCollection AddAchievementFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IAchievementService, AchievementService>();
        services.AddScoped<AchievementSeeder>();
        return services;
    }
}
