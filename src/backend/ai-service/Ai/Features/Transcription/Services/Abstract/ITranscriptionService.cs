using Sellevate.Ai.Features.Transcription.Models;

namespace Sellevate.Ai.Features.Transcription.Services.Abstract;

public interface ITranscriptionService
{
    Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
