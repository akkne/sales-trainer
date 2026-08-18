using Sellevate.Ai.Features.Transcription.Models;

namespace Sellevate.Ai.Features.Transcription.Services.Abstract;

/// <summary>
/// Speech-to-text for an audio stream the caller owns.
/// </summary>
public interface ITranscriptionService
{
    /// <summary>
    /// Transcribes <paramref name="audioStream"/>. <paramref name="fileName"/> is not read from disk —
    /// only its extension is, to pick the MIME type the upload is labelled with, so it must carry the
    /// real format of the bytes.
    ///
    /// <para>
    /// Returns a stub transcript rather than throwing when no provider key is configured. Throws
    /// <see cref="InvalidOperationException"/> when the provider rejects the upload or answers
    /// something unreadable; the provider's own body never reaches the exception.
    /// </para>
    /// </summary>
    Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
