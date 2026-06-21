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
}
