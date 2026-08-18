namespace Sellevate.Ai.Features.Voice.Services.Abstract;

/// <summary>
/// The one door to speech synthesis. Callers never reach a provider directly, so the caching
/// decorator and the spend meter cannot be bypassed.
/// </summary>
public interface ITtsRouter
{
    /// <summary>
    /// <see langword="false"/> when no provider key is configured. The whole voice surface gates on
    /// this rather than on a feature flag, so an enabled-but-unconfigured deployment serves text.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Synthesizes <paramref name="text"/> as a seekable WAV stream the caller owns and must dispose.
    /// Throws <see cref="InvalidOperationException"/> when no provider is configured.
    /// </summary>
    Task<Stream> SynthesizeSpeechAsync(string text, string? modeVoiceId, CancellationToken cancellationToken = default);
}
