namespace SalesTrainer.Api.Features.Voice.Services.Abstract;

public interface IVoiceDialogService
{
    Task<Stream> ProcessVoiceMessageAsync(
        string sessionId,
        Guid userId,
        string transcript,
        CancellationToken ct = default);

    /// <summary>
    /// Streams the AI response sentence-by-sentence: each yielded byte[] is a
    /// self-contained MP3 produced by the configured TTS provider for a single
    /// sentence boundary. Callers append the chunks to an audio queue to start
    /// playback before the full LLM response is finished.
    /// </summary>
    IAsyncEnumerable<VoiceStreamChunk> StreamVoiceMessageAsync(
        string sessionId,
        Guid userId,
        string transcript,
        CancellationToken ct = default);
}

public sealed record VoiceStreamChunk(string Text, byte[] AudioMp3, bool IsStopSignal, bool IsFinal);
