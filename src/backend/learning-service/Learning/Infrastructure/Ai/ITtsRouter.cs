namespace Sellevate.Learning.Infrastructure.Ai;

public interface ITtsRouter
{
    bool IsConfigured { get; }
    Task<Stream> SynthesizeSpeechAsync(string text, string? modeVoiceId, CancellationToken cancellationToken = default);
}
