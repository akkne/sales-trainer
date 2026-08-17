namespace Sellevate.Learning.Eventing;

public interface ILearningEventPublisher
{
    Task PublishExerciseCompletedAsync(ExerciseCompletedEvent payload, CancellationToken cancellationToken = default);

    Task PublishLessonCompletedAsync(LessonCompletedEvent payload, CancellationToken cancellationToken = default);

    Task PublishSkillCompletedAsync(SkillCompletedEvent payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 40.23. Stages the three assignment notices. Every one of them goes through the outbox
    /// like the three events above, so "this person was asked" (their progress row) and "this person
    /// was told" (the event) are committed together or not at all — a fan-out that wrote rows and
    /// then failed to publish would leave people holding work nobody told them about.
    /// </summary>
    Task PublishAssignmentIssuedAsync(AssignmentIssuedEvent payload, CancellationToken cancellationToken = default);

    Task PublishAssignmentDeadlineApproachingAsync(
        AssignmentDeadlineApproachingEvent payload,
        CancellationToken cancellationToken = default);

    Task PublishAssignmentReminderAsync(
        AssignmentReminderEvent payload,
        CancellationToken cancellationToken = default);
}
