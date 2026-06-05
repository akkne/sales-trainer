namespace SalesTrainer.Api.Features.Voice.Services.Abstract;

public interface IVoiceDialogService
{
    IAsyncEnumerable<VoiceStreamChunk> StreamVoiceMessageAsync(
        string sessionId,
        Guid userId,
        string transcript,
        CancellationToken ct = default);
}

public sealed record VoiceStreamChunk(string Text, byte[] AudioMp3, bool IsStopSignal, bool IsFinal);
