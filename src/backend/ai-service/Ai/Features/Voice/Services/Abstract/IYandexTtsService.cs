namespace Sellevate.Ai.Features.Voice.Services.Abstract;

/// <summary>
/// Yandex SpeechKit synthesis. Reached only through <see cref="ITtsRouter"/> in production code:
/// calling it directly skips the audio cache and pays for a phrase that was already synthesized.
/// </summary>
public interface IYandexTtsService
{
    /// <summary><see langword="false"/> when the key is absent or still a placeholder.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Synthesizes <paramref name="text"/> as a WAV stream the caller owns. Charged in characters at
    /// this level, so a cache hit above it costs nothing.
    /// </summary>
    Task<Stream> SynthesizeSpeechAsync(string text, string? voice = null, CancellationToken cancellationToken = default);
}
