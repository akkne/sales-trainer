namespace SalesTrainer.Api.Features.Voice.Services.Abstract;

public interface IGoogleTtsService
{
    bool IsConfigured { get; }
    Task<Stream> SynthesizeSpeechAsync(string text, string? voiceName = null, CancellationToken cancellationToken = default);
}
