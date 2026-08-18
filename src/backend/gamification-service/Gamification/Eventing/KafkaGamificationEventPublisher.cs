using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Outbox;

namespace Sellevate.Gamification.Eventing;

/// <summary>
/// Writes gamification's outgoing events into the transactional outbox, from where the relay forwards
/// them to Kafka.
///
/// <para>
/// Nothing here reaches the broker: each method enqueues and returns, so an event ships only if the
/// caller's transaction commits. The methods are synchronous work behind an async signature for that
/// reason, and the interface keeps the signature so a future direct-producer implementation needs no
/// caller changes.
/// </para>
///
/// <para>
/// Per-user events are partitioned by user id, which keeps one user's grants, streak milestones and
/// achievements in order relative to each other. The dialog-weights event is installation-wide and has
/// no user, so it is partitioned by its own topic name — every such event lands on one partition and
/// therefore in order, which matters because a later weight update must not be overtaken by an earlier
/// one.
/// </para>
/// </summary>
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
