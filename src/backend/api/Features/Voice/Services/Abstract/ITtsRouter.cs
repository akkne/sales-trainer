namespace SalesTrainer.Api.Features.Voice.Services.Abstract;

public interface ITtsRouter
{
    bool IsConfigured { get; }
    Task<Stream> SynthesizeSpeechAsync(string text, string? modeVoiceId, CancellationToken cancellationToken = default);
}
