namespace Sellevate.Learning.Infrastructure.Ai;

internal interface IYandexTtsService
{
    bool IsConfigured { get; }
    Task<Stream> SynthesizeSpeechAsync(string text, string? voice = null, CancellationToken cancellationToken = default);
}
