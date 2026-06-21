using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;

namespace Sellevate.Learning.Eventing;

internal sealed class KafkaLearningEventPublisher(IEventPublisher eventPublisher) : ILearningEventPublisher
{
    public Task PublishExerciseCompletedAsync(ExerciseCompletedEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.ExerciseCompleted, payload.UserId.ToString(), Topics.ExerciseCompleted, payload, cancellationToken: cancellationToken);

    public Task PublishLessonCompletedAsync(LessonCompletedEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.LessonCompleted, payload.UserId.ToString(), Topics.LessonCompleted, payload, cancellationToken: cancellationToken);

    public Task PublishSkillCompletedAsync(SkillCompletedEvent payload, CancellationToken cancellationToken = default) =>
        eventPublisher.PublishAsync(
            Topics.SkillCompleted, payload.UserId.ToString(), Topics.SkillCompleted, payload, cancellationToken: cancellationToken);
}
