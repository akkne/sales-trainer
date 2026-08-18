namespace Sellevate.Ai.Features.Voice.Services.Abstract;

/// <summary>
/// One learner utterance in, an interleaved stream of reply text and synthesized audio out.
/// </summary>
public interface IVoiceDialogService
{
    /// <summary>
    /// Streams the character's answer. Text chunks arrive before the audio of the same words, and a
    /// chunk carries either text or audio, never both. The final chunk is empty and carries only
    /// <c>IsFinal</c>; <c>IsStopSignal</c> marks the character hanging up.
    ///
    /// <para>
    /// Throws <see cref="InvalidOperationException"/> before the first chunk when the session is
    /// missing, not active, or its mode has voice disabled — the caller may still turn that into a
    /// status code. A synthesis failure does not throw: the turn continues as text only.
    /// </para>
    /// </summary>
    IAsyncEnumerable<VoiceStreamChunk> StreamVoiceMessageAsync(
        string sessionId,
        Guid userId,
        string transcript,
        CancellationToken cancellationToken = default);
}
