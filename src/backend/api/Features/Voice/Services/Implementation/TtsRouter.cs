using SalesTrainer.Api.Features.Voice.Services.Abstract;

namespace SalesTrainer.Api.Features.Voice.Services.Implementation;

internal sealed class TtsRouter : ITtsRouter
{
    private readonly IYandexTtsService _yandexTtsService;
    private readonly IVoicerTtsService _voicerTtsService;
    private readonly IGoogleTtsService _googleTtsService;
    private readonly IConfiguration _configuration;

    public TtsRouter(
        IYandexTtsService yandexTtsService,
        IVoicerTtsService voicerTtsService,
        IGoogleTtsService googleTtsService,
        IConfiguration configuration)
    {
        _yandexTtsService = yandexTtsService;
        _voicerTtsService = voicerTtsService;
        _googleTtsService = googleTtsService;
        _configuration = configuration;
    }

    private string ActiveProvider
    {
        get
        {
            var preferred = (_configuration["Voice:TtsProvider"] ?? "yandex").Trim().ToLowerInvariant();
            return preferred switch
            {
                "yandex" when _yandexTtsService.IsConfigured => "yandex",
                "voicer" when _voicerTtsService.IsConfigured => "voicer",
                "google" when _googleTtsService.IsConfigured => "google",
                // Preferred provider lacks credentials — fall back in latency order.
                _ when _yandexTtsService.IsConfigured => "yandex",
                _ when _googleTtsService.IsConfigured => "google",
                _ when _voicerTtsService.IsConfigured => "voicer",
                _ => "none",
            };
        }
    }

    public bool IsConfigured => ActiveProvider != "none";

    public bool IsRealtime => ActiveProvider is "yandex" or "google";

    public Task<Stream> SynthesizeSpeechAsync(string text, string? modeVoiceId, CancellationToken cancellationToken = default)
    {
        return ActiveProvider switch
        {
            // Mode-level VoiceId values are ElevenLabs voice ids — meaningful only
            // for Voicer/Google routing; Yandex voices come from YandexTts:Voice.
            "yandex" => _yandexTtsService.SynthesizeSpeechAsync(text, voice: null, cancellationToken),
            "voicer" => _voicerTtsService.SynthesizeSpeechAsync(text, modeVoiceId, cancellationToken),
            "google" => _googleTtsService.SynthesizeSpeechAsync(text, modeVoiceId, cancellationToken),
            _ => throw new InvalidOperationException(
                "No TTS provider is configured. Set Voice:TtsProvider and the matching API key."),
        };
    }
}
