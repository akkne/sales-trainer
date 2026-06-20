namespace Sellevate.Learning.Eventing;

public interface ILearningEventPublisher
{
    Task PublishExerciseCompletedAsync(ExerciseCompletedEvent payload, CancellationToken cancellationToken = default);

    Task PublishLessonCompletedAsync(LessonCompletedEvent payload, CancellationToken cancellationToken = default);

    Task PublishSkillCompletedAsync(SkillCompletedEvent payload, CancellationToken cancellationToken = default);

    Task PublishTechniqueMasteryChangedAsync(TechniqueMasteryChangedEvent payload, CancellationToken cancellationToken = default);
}
