using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Outbox;

namespace Sellevate.Learning.Eventing;

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
