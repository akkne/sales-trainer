namespace SalesTrainer.Api.Features.Voice;

public interface IGoogleTtsService
{
    bool IsConfigured { get; }
    Task<Stream> SynthesizeSpeechAsync(string text, string? voiceName = null, CancellationToken ct = default);
}
