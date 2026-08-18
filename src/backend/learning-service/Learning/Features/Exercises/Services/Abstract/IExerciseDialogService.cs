using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Infrastructure.Ai;

namespace Sellevate.Learning.Features.Exercises.Services.Abstract;

/// <summary>
/// The interactive half of <c>ai_dialogue</c>: a turn-limited practice conversation, delivered as text
/// or as a voice stream.
///
/// <para>
/// Conversation state is per learner and per exercise and lives only in a cache with a bounded
/// lifetime, so a caller must treat "the conversation restarted" as a normal outcome rather than an
/// error. Neither transport records anything in the learner's progress — practising a dialogue is not
/// submitting an answer.
/// </para>
/// </summary>
public interface IExerciseDialogService
{
    /// <summary>
    /// Validates that the exercise exists and is of type ai_dialogue.
    /// Throws <see cref="KeyNotFoundException"/> if not found,
    /// or <see cref="NotSupportedException"/> if the wrong type.
    /// Call this before committing an HTTP 200 on streaming endpoints.
    /// </summary>
    Task ValidateExerciseForVoiceAsync(Guid exerciseId, CancellationToken cancellationToken = default);

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
}
