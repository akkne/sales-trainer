namespace SalesTrainer.Api.Features.Voice;

public interface IVoicerTtsService
{
    bool IsConfigured { get; }
    Task<Stream> SynthesizeSpeechAsync(string text, string? voiceId = null, CancellationToken ct = default);
}
