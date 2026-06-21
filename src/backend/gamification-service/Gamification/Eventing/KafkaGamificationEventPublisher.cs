using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Outbox;

namespace Sellevate.Gamification.Eventing;

internal sealed class KafkaGamificationEventPublisher(IOutboxWriter outboxWriter) : IGamificationEventPublisher
{
    public Task PublishExperiencePointsGrantedAsync(ExperiencePointsGrantedEvent payload, CancellationToken cancellationToken = default)
    {
        outboxWriter.Enqueue(Topics.XpGranted, payload.UserId.ToString(), Topics.XpGranted, payload);
        return Task.CompletedTask;
    }

    public Task PublishAchievementUnlockedAsync(AchievementUnlockedEvent payload, CancellationToken cancellationToken = default)
    {
        outboxWriter.Enqueue(Topics.AchievementUnlocked, payload.UserId.ToString(), Topics.AchievementUnlocked, payload);
        return Task.CompletedTask;
    }

    public Task PublishStreakMilestoneAsync(StreakMilestoneEvent payload, CancellationToken cancellationToken = default)
    {
        outboxWriter.Enqueue(Topics.StreakMilestone, payload.UserId.ToString(), Topics.StreakMilestone, payload);
        return Task.CompletedTask;
    }

    public Task PublishLeagueUpdatedAsync(LeagueUpdatedEvent payload, CancellationToken cancellationToken = default)
    {
        outboxWriter.Enqueue(Topics.LeagueUpdated, payload.UserId.ToString(), Topics.LeagueUpdated, payload);
        return Task.CompletedTask;
    }

    public Task PublishDialogWeightsUpdatedAsync(GamificationDialogWeightsUpdatedEvent payload, CancellationToken cancellationToken = default)
    {
        outboxWriter.Enqueue(
            Topics.GamificationDialogWeightsUpdated,
            Topics.GamificationDialogWeightsUpdated,
            Topics.GamificationDialogWeightsUpdated,
            payload);
        return Task.CompletedTask;
    }
}
