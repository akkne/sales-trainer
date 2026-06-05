namespace SalesTrainer.Api.Features.Voice.Services.Abstract;

/// <summary>
/// Resolves the active TTS provider (Voice:TtsProvider with fallback) and routes
/// synthesis calls to it. Single source of truth for "is voice available" checks.
/// </summary>
public interface ITtsRouter
{
    /// <summary>True when at least one TTS provider has real credentials.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// True when the active provider answers in about a second (synchronous APIs:
    /// Yandex, Google) — the dialog pipeline then synthesizes per sentence.
    /// False for queue-based providers (Voicer, ~10-35s/task) — the pipeline then
    /// batches the whole reply into one task.
    /// </summary>
    bool IsRealtime { get; }

    Task<Stream> SynthesizeSpeechAsync(string text, string? modeVoiceId, CancellationToken cancellationToken = default);
}
