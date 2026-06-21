using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Infrastructure.Ai;

namespace Sellevate.Learning.Features.Exercises.Services.Abstract;

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
