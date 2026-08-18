using System.Text.Json;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Infrastructure.Ai;

namespace Sellevate.Learning.Features.Exercises.Services.Abstract;

/// <summary>
/// Everything a learner does with the library: browsing lessons, reading exercises, submitting answers,
/// and holding an <c>ai_dialogue</c> conversation.
///
/// <para>
/// Every method is scoped to the caller's organization by the ambient tenant context and takes no
/// organization argument. The lesson and exercise bodies it returns are already rendered for that
/// organization and already stripped of their answer keys, so a caller must never reach for the raw
/// rows to "get the full content" — that is the answer key.
/// </para>
/// </summary>
public interface IExerciseService
{
    Task<IReadOnlyList<LessonSummaryDto>> GetAllLessonsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LessonSummaryDto>> GetLessonsForTopicAsync(
        Guid userId,
        Guid topicId,
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

    Task<ExerciseChatResponseDto> SendChatMessageAsync(
        Guid userId,
        Guid exerciseId,
        string userMessage,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<VoiceStreamChunk> StreamExerciseVoiceAsync(
        Guid userId,
        Guid exerciseId,
        string transcript,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that the exercise exists and supports voice streaming.
    /// Throws <see cref="KeyNotFoundException"/> or <see cref="NotSupportedException"/>.
    /// Call before committing HTTP 200 on the voice stream endpoint.
    /// </summary>
    Task ValidateExerciseForVoiceAsync(Guid exerciseId, CancellationToken cancellationToken = default);
}
