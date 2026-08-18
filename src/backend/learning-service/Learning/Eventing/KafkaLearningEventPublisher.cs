using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Outbox;

namespace Sellevate.Learning.Eventing;

/// <summary>
/// Stages every outgoing learning event through the transactional outbox instead of producing to
/// Kafka directly, so an event is never visible unless the rows it describes committed with it.
///
/// <para>
/// <b>The partition key is always the recipient.</b> Per-recipient ordering exists only if the
/// recipient is the key, so learner-facing notices are keyed by the learner and the two РОП-facing
/// ones — the deadline digest and a filed dispute — by the administrator, not by the assignment.
/// </para>
/// </summary>
internal sealed class KafkaLearningEventPublisher(IOutboxWriter outboxWriter) : ILearningEventPublisher
{
    public Task PublishExerciseCompletedAsync(ExerciseCompletedEvent payload, CancellationToken cancellationToken = default)
    {
        outboxWriter.Enqueue(Topics.ExerciseCompleted, payload.UserId.ToString(), Topics.ExerciseCompleted, payload);
        return Task.CompletedTask;
    }

    public Task PublishLessonCompletedAsync(LessonCompletedEvent payload, CancellationToken cancellationToken = default)
    {
        outboxWriter.Enqueue(Topics.LessonCompleted, payload.UserId.ToString(), Topics.LessonCompleted, payload);
        return Task.CompletedTask;
    }

    public Task PublishSkillCompletedAsync(SkillCompletedEvent payload, CancellationToken cancellationToken = default)
    {
        outboxWriter.Enqueue(Topics.SkillCompleted, payload.UserId.ToString(), Topics.SkillCompleted, payload);
        return Task.CompletedTask;
    }

    public Task PublishAssignmentIssuedAsync(
        AssignmentIssuedEvent payload,
        CancellationToken cancellationToken = default)
    {
        outboxWriter.Enqueue(Topics.AssignmentIssued, payload.UserId.ToString(), Topics.AssignmentIssued, payload);
        return Task.CompletedTask;
    }

    public Task PublishAssignmentDeadlineApproachingAsync(
        AssignmentDeadlineApproachingEvent payload,
        CancellationToken cancellationToken = default)
    {
        outboxWriter.Enqueue(
            Topics.AssignmentDeadlineApproaching,
            payload.UserId.ToString(),
            Topics.AssignmentDeadlineApproaching,
            payload);

        return Task.CompletedTask;
    }

    public Task PublishAssignmentReminderAsync(
        AssignmentReminderEvent payload,
        CancellationToken cancellationToken = default)
    {
        outboxWriter.Enqueue(Topics.AssignmentReminder, payload.UserId.ToString(), Topics.AssignmentReminder, payload);
        return Task.CompletedTask;
    }

    public Task PublishAssignmentDeadlineDigestAsync(
        AssignmentDeadlineDigestEvent payload,
        CancellationToken cancellationToken = default)
    {
        outboxWriter.Enqueue(
            Topics.AssignmentDeadlineDigest,
            payload.AdministratorUserId.ToString(),
            Topics.AssignmentDeadlineDigest,
            payload);

        return Task.CompletedTask;
    }

    public Task PublishDialogReviewDisputedAsync(
        DialogReviewDisputedEvent payload,
        CancellationToken cancellationToken = default)
    {
        outboxWriter.Enqueue(
            Topics.DialogReviewDisputed,
            payload.AdministratorUserId.ToString(),
            Topics.DialogReviewDisputed,
            payload);

        return Task.CompletedTask;
    }

    public Task PublishAssignmentProgressChangedAsync(
        AssignmentProgressChangedEvent payload,
        CancellationToken cancellationToken = default)
    {
        outboxWriter.Enqueue(
            Topics.AssignmentProgressChanged,
            payload.UserId.ToString(),
            Topics.AssignmentProgressChanged,
            payload);

        return Task.CompletedTask;
    }

    public Task PublishDialogReviewCommentedAsync(
        DialogReviewCommentedEvent payload,
        CancellationToken cancellationToken = default)
    {
        outboxWriter.Enqueue(
            Topics.DialogReviewCommented, payload.UserId.ToString(), Topics.DialogReviewCommented, payload);

        return Task.CompletedTask;
    }

    public Task PublishDialogReviewResolvedAsync(
        DialogReviewResolvedEvent payload,
        CancellationToken cancellationToken = default)
    {
        outboxWriter.Enqueue(
            Topics.DialogReviewResolved, payload.UserId.ToString(), Topics.DialogReviewResolved, payload);

        return Task.CompletedTask;
    }
}
