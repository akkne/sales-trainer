using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Infrastructure.Ai;

namespace Sellevate.Learning.Features.Exercises.Services.Abstract;

public interface IExerciseDialogService
{
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
