namespace SalesTrainer.Api.Features.Voice;

public interface IElevenLabsService
{
    bool IsConfigured { get; }
    Task<Stream> SynthesizeSpeechAsync(string text, string? voiceId = null, CancellationToken ct = default);
}
