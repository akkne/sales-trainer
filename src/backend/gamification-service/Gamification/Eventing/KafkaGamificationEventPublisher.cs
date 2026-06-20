using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;

namespace Sellevate.Gamification.Eventing;

internal sealed class KafkaGamificationEventPublisher(IEventPublisher eventPublisher) : IGamificationEventPublisher
{
    public Task PublishExperiencePointsGrantedAsync(ExperiencePointsGrantedEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.XpGranted, payload.UserId.ToString(), Topics.XpGranted, payload, cancellationToken: cancellationToken);

    public Task PublishAchievementUnlockedAsync(AchievementUnlockedEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.AchievementUnlocked, payload.UserId.ToString(), Topics.AchievementUnlocked, payload, cancellationToken: cancellationToken);

    public Task PublishStreakMilestoneAsync(StreakMilestoneEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.StreakMilestone, payload.UserId.ToString(), Topics.StreakMilestone, payload, cancellationToken: cancellationToken);

    public Task PublishDialogWeightsUpdatedAsync(GamificationDialogWeightsUpdatedEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.GamificationDialogWeightsUpdated,
            Topics.GamificationDialogWeightsUpdated,
            Topics.GamificationDialogWeightsUpdated,
            payload,
            cancellationToken: cancellationToken);
}
