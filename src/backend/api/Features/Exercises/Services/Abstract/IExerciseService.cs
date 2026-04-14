using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;

namespace SalesTrainer.Api.Features.Exercises.Services.Abstract;

public interface IExerciseService
{
    Task<IReadOnlyList<LessonSummaryDto>> GetAllLessonsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LessonSummaryDto>> GetLessonsForTopicAsync(
        Guid userId,
        Guid topicId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExerciseDto>> GetExercisesForLessonAsync(
        Guid lessonId,
        CancellationToken cancellationToken = default);

    Task<ExerciseSubmissionResultDto> SubmitExerciseAnswerAsync(
        Guid userId,
        Guid exerciseId,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles a chat turn in an ai_dialog exercise.
    /// Returns the AI response and conversation state.
    /// </summary>
    Task<ExerciseChatResponseDto> SendChatMessageAsync(
        Guid userId,
        Guid exerciseId,
        string userMessage,
        CancellationToken cancellationToken = default);
}
