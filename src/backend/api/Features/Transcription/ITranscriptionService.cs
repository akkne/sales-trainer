namespace SalesTrainer.Api.Features.Transcription;

public interface ITranscriptionService
{
    /// <summary>
    /// Transcribes audio file content using OpenAI Whisper API.
    /// </summary>
    /// <param name="audioStream">Audio file stream (mp3, mp4, mpeg, mpga, m4a, wav, webm).</param>
    /// <param name="fileName">Original file name including extension (used to determine MIME type).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transcription result with text and detected language.</returns>
    Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream,
        string fileName,
        CancellationToken cancellationToken = default);
}

public record TranscriptionResult(string Text, string? Language);
