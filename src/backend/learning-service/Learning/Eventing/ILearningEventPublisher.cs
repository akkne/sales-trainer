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

    /// <summary>
    /// Phase 40.26. Stages the two РОП-facing notices — the deadline digest and a filed dispute.
    /// Through the outbox like everything above, and for the digest that matters more than usual: the
    /// timestamp that records the digest as announced is written in the same transaction, so a crash
    /// between the two cannot produce an assignment that is marked as announced to nobody.
    /// </summary>
    Task PublishAssignmentDeadlineDigestAsync(
        AssignmentDeadlineDigestEvent payload,
        CancellationToken cancellationToken = default);

    Task PublishDialogReviewDisputedAsync(
        DialogReviewDisputedEvent payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 40.25. One progress row changed state. Staged in the same transaction as the change,
    /// so the funnel counter analytics-service keeps cannot drift from the rows it describes.
    /// </summary>
    Task PublishAssignmentProgressChangedAsync(
        AssignmentProgressChangedEvent payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 40.25. The two notices of the feedback loop (docs/TENANCY/ASSIGNMENTS.md §4.1). Through
    /// the outbox like everything above, so the row and the notice about it commit together — a
    /// coaching note that existed without being delivered would be the РОП believing they had spoken
    /// to somebody who never heard them.
    /// </summary>
    Task PublishDialogReviewCommentedAsync(
        DialogReviewCommentedEvent payload,
        CancellationToken cancellationToken = default);

    Task PublishDialogReviewResolvedAsync(
        DialogReviewResolvedEvent payload,
        CancellationToken cancellationToken = default);
}
