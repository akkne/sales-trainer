namespace Sellevate.Gamification.Eventing;

/// <summary>
/// Publishes gamification's outgoing integration events.
///
/// <para>
/// <b>Enqueues, does not send.</b> Every implementation writes to the transactional outbox in the
/// caller's <c>DbContext</c>, so an event becomes visible only if the transaction that produced it
/// commits — there is no window in which a notification exists for a grant that was rolled back.
/// Callers must therefore still call <c>SaveChangesAsync</c>; publishing alone delivers nothing.
/// </para>
/// </summary>
public interface IGamificationEventPublisher
{
    Task PublishExperiencePointsGrantedAsync(ExperiencePointsGrantedEvent payload, CancellationToken cancellationToken = default);

    Task PublishAchievementUnlockedAsync(AchievementUnlockedEvent payload, CancellationToken cancellationToken = default);

    Task PublishStreakMilestoneAsync(StreakMilestoneEvent payload, CancellationToken cancellationToken = default);

    Task PublishDialogWeightsUpdatedAsync(GamificationDialogWeightsUpdatedEvent payload, CancellationToken cancellationToken = default);
}
