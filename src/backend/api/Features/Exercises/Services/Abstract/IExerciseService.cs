using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;

namespace SalesTrainer.Api.Features.Exercises.Services.Abstract;

public interface IExerciseService
{
    Task<IReadOnlyList<LessonSummaryDto>> GetAllLessonsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LessonSummaryDto>> GetLessonsForSkillAsync(
        Guid userId,
        string skillSlug,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExerciseDto>> GetExercisesForLessonAsync(
        Guid lessonId,
        CancellationToken cancellationToken = default);

    Task<ExerciseSubmissionResultDto> SubmitExerciseAnswerAsync(
        Guid userId,
        Guid exerciseId,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default);

    Task<NextLessonDto?> GetNextAvailableLessonAsync(
        Guid userId,
        Guid lessonId,
        CancellationToken cancellationToken = default);
}
