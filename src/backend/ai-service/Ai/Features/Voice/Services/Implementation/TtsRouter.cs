using Sellevate.Ai.Features.Voice.Services.Abstract;

namespace Sellevate.Ai.Features.Voice.Services.Implementation;

/// <summary>
/// Selects the speech provider for a synthesis request. One provider exists today, so the routing
/// reduces to "Yandex if its key is configured, otherwise nothing".
///
/// <para>
/// <see cref="IsConfigured"/> is what the whole voice surface gates on. It is derived from the
/// providers rather than from a setting, so a deployment that enabled voice without supplying a key
/// serves text-only rather than failing every turn.
/// </para>
/// </summary>
internal sealed class TtsRouter : ITtsRouter
{
    private const string YandexProvider = "yandex";
    private const string NoProvider = "none";

    private readonly IYandexTtsService _yandexTtsService;

    public TtsRouter(IYandexTtsService yandexTtsService)
    {
        _yandexTtsService = yandexTtsService;
    }

    private string ActiveProvider => _yandexTtsService.IsConfigured ? YandexProvider : NoProvider;

    public bool IsConfigured => ActiveProvider != NoProvider;

    public Task<Stream> SynthesizeSpeechAsync(string text, string? modeVoiceId, CancellationToken cancellationToken = default)
    {
        return ActiveProvider switch
        {
            YandexProvider => _yandexTtsService.SynthesizeSpeechAsync(text, voice: null, cancellationToken),
            _ => throw new InvalidOperationException("No TTS provider is configured. Set Voice:TtsProvider and the matching API key."),
        };
    }
}
