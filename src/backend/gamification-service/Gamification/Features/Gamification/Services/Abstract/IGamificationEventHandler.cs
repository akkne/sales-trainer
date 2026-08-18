namespace Sellevate.Gamification.Features.Gamification.Services.Abstract;

/// <summary>
/// Translates one learning or dialog event into every gamification consequence it has: the
/// experience-point grant, the streak registration, and the achievement re-evaluation.
///
/// <para>
/// The fan-out lives here rather than in the Kafka consumers so that all three consequences of an
/// event stay in one place and cannot drift apart per topic. Each method is safe to call twice for the
/// same event as long as the event id is passed through.
/// </para>
/// </summary>
public interface IGamificationEventHandler
{
    Task HandleExerciseCompletedAsync(Guid userId, string exerciseType, bool isCorrect, Guid? sourceEventId = null, CancellationToken cancellationToken = default);

    Task HandleDialogEvaluatedAsync(Guid userId, int experiencePointsEarned, Guid? sourceEventId = null, CancellationToken cancellationToken = default);

    Task HandleLessonCompletedAsync(Guid userId, CancellationToken cancellationToken = default);

    Task HandleSkillCompletedAsync(Guid userId, CancellationToken cancellationToken = default);
}
