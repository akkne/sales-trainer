namespace Sellevate.Learning.Infrastructure.Ai;

internal sealed class TtsRouter : ITtsRouter
{
    private readonly IYandexTtsService _yandexTtsService;

    public TtsRouter(IYandexTtsService yandexTtsService)
    {
        _yandexTtsService = yandexTtsService;
    }

    private string ActiveProvider => _yandexTtsService.IsConfigured ? "yandex" : "none";

    public bool IsConfigured => ActiveProvider != "none";

    public Task<Stream> SynthesizeSpeechAsync(string text, string? modeVoiceId, CancellationToken cancellationToken = default)
    {
        return ActiveProvider switch
        {
            "yandex" => _yandexTtsService.SynthesizeSpeechAsync(text, voice: null, cancellationToken),
            _ => throw new InvalidOperationException("No TTS provider is configured. Set Voice:TtsProvider and the matching API key."),
        };
    }
}
