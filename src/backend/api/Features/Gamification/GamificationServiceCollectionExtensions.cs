using SalesTrainer.Api.Features.Gamification.Services.Abstract;
using SalesTrainer.Api.Features.Gamification.Services.Implementation;

namespace SalesTrainer.Api.Features.Gamification;

public static class GamificationServiceCollectionExtensions
{
    public static IServiceCollection AddGamificationFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<StreakResetJob>();
        services.AddScoped<IGamificationService, GamificationService>();
        return services;
    }
}
