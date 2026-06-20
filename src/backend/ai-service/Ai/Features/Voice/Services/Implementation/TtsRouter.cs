using Microsoft.Extensions.Options;
using Sellevate.Ai.Features.Voice.Services.Abstract;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Voice.Services.Implementation;

internal sealed class TtsRouter : ITtsRouter
{
    private readonly IYandexTtsService _yandexTtsService;
    private readonly IGoogleTtsService _googleTtsService;
    private readonly IOptions<TtsRouterConfiguration> _ttsRouterOptions;

    public TtsRouter(
        IYandexTtsService yandexTtsService,
        IGoogleTtsService googleTtsService,
        IOptions<TtsRouterConfiguration> ttsRouterOptions)
    {
        _yandexTtsService = yandexTtsService;
        _googleTtsService = googleTtsService;
        _ttsRouterOptions = ttsRouterOptions;
    }

    private string ActiveProvider
    {
        get
        {
            var preferred = _ttsRouterOptions.Value.TtsProvider.Trim().ToLowerInvariant();
            return preferred switch
            {
                "yandex" when _yandexTtsService.IsConfigured => "yandex",
                "google" when _googleTtsService.IsConfigured => "google",
                _ when _yandexTtsService.IsConfigured => "yandex",
                _ when _googleTtsService.IsConfigured => "google",
                _ => "none",
            };
        }
    }

    public bool IsConfigured => ActiveProvider != "none";

    public Task<Stream> SynthesizeSpeechAsync(string text, string? modeVoiceId, CancellationToken cancellationToken = default)
    {
        return ActiveProvider switch
        {
            "yandex" => _yandexTtsService.SynthesizeSpeechAsync(text, voice: null, cancellationToken),
            "google" => _googleTtsService.SynthesizeSpeechAsync(text, modeVoiceId, cancellationToken),
            _ => throw new InvalidOperationException("No TTS provider is configured. Set Voice:TtsProvider and the matching API key."),
        };
    }
}
