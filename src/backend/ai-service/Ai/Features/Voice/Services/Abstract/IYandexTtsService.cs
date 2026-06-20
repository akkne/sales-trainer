namespace Sellevate.Ai.Features.Voice.Services.Abstract;

public interface IYandexTtsService
{
    bool IsConfigured { get; }
    Task<Stream> SynthesizeSpeechAsync(string text, string? voice = null, CancellationToken cancellationToken = default);
}
