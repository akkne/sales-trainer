using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Voice.Services.Abstract;

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

    /// <summary>
    /// Handles a chat turn in an ai_dialog exercise.
    /// Returns the AI response and conversation state.
    /// </summary>
    Task<ExerciseChatResponseDto> SendChatMessageAsync(
        Guid userId,
        Guid exerciseId,
        string userMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams a voice turn for an ai_dialog exercise: text + MP3 audio frames,
    /// using the same TTS pipeline as live calls. Shares the chat history with
    /// <see cref="SendChatMessageAsync"/> so text and voice turns interleave.
    /// </summary>
    IAsyncEnumerable<VoiceStreamChunk> StreamExerciseVoiceAsync(
        Guid userId,
        Guid exerciseId,
        string transcript,
        CancellationToken cancellationToken = default);
}
