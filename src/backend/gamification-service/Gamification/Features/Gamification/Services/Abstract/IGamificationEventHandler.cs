namespace Sellevate.Gamification.Features.Gamification.Services.Abstract;

public interface IGamificationEventHandler
{
    Task HandleExerciseCompletedAsync(Guid userId, string exerciseType, bool isCorrect, CancellationToken cancellationToken = default);

    Task HandleDialogEvaluatedAsync(Guid userId, int experiencePointsEarned, CancellationToken cancellationToken = default);

    Task HandleLessonCompletedAsync(Guid userId, CancellationToken cancellationToken = default);

    Task HandleSkillCompletedAsync(Guid userId, CancellationToken cancellationToken = default);
}
