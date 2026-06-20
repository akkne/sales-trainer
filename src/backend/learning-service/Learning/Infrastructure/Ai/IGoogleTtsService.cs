namespace Sellevate.Learning.Infrastructure.Ai;

internal interface IGoogleTtsService
{
    bool IsConfigured { get; }
    Task<Stream> SynthesizeSpeechAsync(string text, string? voiceName = null, CancellationToken cancellationToken = default);
}
