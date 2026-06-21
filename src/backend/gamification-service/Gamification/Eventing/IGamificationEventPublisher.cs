namespace Sellevate.Gamification.Eventing;

public interface IGamificationEventPublisher
{
    Task PublishExperiencePointsGrantedAsync(ExperiencePointsGrantedEvent payload, CancellationToken cancellationToken = default);

    Task PublishAchievementUnlockedAsync(AchievementUnlockedEvent payload, CancellationToken cancellationToken = default);

    Task PublishStreakMilestoneAsync(StreakMilestoneEvent payload, CancellationToken cancellationToken = default);

    Task PublishLeagueUpdatedAsync(LeagueUpdatedEvent payload, CancellationToken cancellationToken = default);

    Task PublishDialogWeightsUpdatedAsync(GamificationDialogWeightsUpdatedEvent payload, CancellationToken cancellationToken = default);
}
