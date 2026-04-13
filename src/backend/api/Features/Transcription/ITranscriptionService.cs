namespace SalesTrainer.Api.Features.Transcription;

public interface ITranscriptionService
{
    Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
