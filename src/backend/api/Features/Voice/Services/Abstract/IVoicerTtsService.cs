namespace SalesTrainer.Api.Features.Voice.Services.Abstract;

public interface IVoicerTtsService
{
    bool IsConfigured { get; }
    Task<Stream> SynthesizeSpeechAsync(string text, string? voiceId = null, CancellationToken cancellationToken = default);
}
