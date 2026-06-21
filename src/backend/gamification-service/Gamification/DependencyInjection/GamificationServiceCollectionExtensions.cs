using Sellevate.BuildingBlocks.Outbox;
using Sellevate.Gamification.Eventing;
using Sellevate.Gamification.Features.Achievements;
using Sellevate.Gamification.Features.Achievements.Services.Abstract;
using Sellevate.Gamification.Features.Achievements.Services.Implementation;
using Sellevate.Gamification.Features.Gamification;
using Sellevate.Gamification.Features.Gamification.Services.Abstract;
using Sellevate.Gamification.Features.Gamification.Services.Implementation;
using Sellevate.Gamification.Features.League;
using Sellevate.Gamification.Features.League.Services.Abstract;
using Sellevate.Gamification.Features.League.Services.Implementation;

namespace Sellevate.Gamification.DependencyInjection;

public static class GamificationServiceCollectionExtensions
{
    public static IServiceCollection AddGamificationServices(this IServiceCollection services)
    {
        services.AddScoped<IGamificationEventPublisher, KafkaGamificationEventPublisher>();
        services.AddScoped<IOutboxWriter, GamificationOutboxWriter>();
        services.AddScoped<IOutboxStore, GamificationOutboxStore>();
        services.AddHostedService<OutboxRelayBackgroundService>();

        services.AddScoped<IGamificationSettingsService, GamificationSettingsService>();
        services.AddScoped<IExperiencePointsGrantService, ExperiencePointsGrantService>();
        services.AddScoped<IStreakService, StreakService>();
        services.AddScoped<IGamificationProgressService, GamificationProgressService>();
        services.AddScoped<IGamificationEventHandler, GamificationEventHandler>();

        services.AddScoped<IAchievementService, AchievementService>();
        services.AddScoped<ILearningProgressService, LearningProgressService>();
        services.AddScoped<AchievementSeeder>();

        services.AddScoped<ILeagueService, LeagueService>();

        services.AddScoped<StreakResetJob>();
        services.AddScoped<WeeklyLeagueClosureJob>();

        services.AddHostedService<UserReplicaConsumer>();
        services.AddHostedService<LearningEventsConsumer>();
        services.AddHostedService<DialogEvaluatedConsumer>();

        return services;
    }
}
