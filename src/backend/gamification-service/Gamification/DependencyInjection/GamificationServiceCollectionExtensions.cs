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
using Sellevate.Gamification.Infrastructure.Configuration;

namespace Sellevate.Gamification.DependencyInjection;

/// <summary>
/// The single registration point for gamification-service's own services, so no lifetime decision
/// is made in <c>Program.cs</c> where it would be invisible next to framework wiring.
/// </summary>
public static class GamificationServiceCollectionExtensions
{
    /// <summary>
    /// Everything is <c>Scoped</c> apart from <see cref="IStreakClock"/>, which is a stateless
    /// singleton because its timezone is read once at construction and never changes, and the hosted
    /// services, which create a scope of their own per unit of work. Nothing scoped is captured by a
    /// singleton here: the Kafka consumers and the outbox relay take
    /// <c>IServiceScopeFactory</c> rather than the scoped services themselves, and so do the two
    /// Hangfire jobs, which are registered scoped and activated per run.
    /// </summary>
    public static IServiceCollection AddGamificationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<StreakConfiguration>(
            configuration.GetSection(StreakConfiguration.SectionName));
        services.Configure<RecurringJobConfiguration>(
            configuration.GetSection(RecurringJobConfiguration.SectionName));

        services.AddSingleton<IStreakClock, StreakClock>();

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
        services.AddScoped<LeagueSettingsSeeder>();

        services.AddScoped<StreakResetJob>();
        services.AddScoped<WeeklyLeagueClosureJob>();

        services.AddHostedService<UserReplicaConsumer>();
        services.AddHostedService<LearningEventsConsumer>();
        services.AddHostedService<DialogEvaluatedConsumer>();

        return services;
    }
}
