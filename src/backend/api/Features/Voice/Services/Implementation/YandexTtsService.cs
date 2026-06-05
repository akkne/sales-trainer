using SalesTrainer.Api.Features.Voice.Models;
using SalesTrainer.Api.Features.Voice.Services.Abstract;

namespace SalesTrainer.Api.Features.Voice.Services.Implementation;

internal sealed class YandexTtsService : IYandexTtsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<YandexTtsService> _logger;

    private const string PlaceholderApiKey = "REPLACE_WITH_YANDEX_API_KEY";
    private const string DefaultBaseUrl = "https://tts.api.cloud.yandex.net";

    public YandexTtsService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<YandexTtsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured
    {
        get
        {
            var apiKey = _configuration["YandexTts:ApiKey"];
            return !string.IsNullOrWhiteSpace(apiKey) &&
                   apiKey != PlaceholderApiKey &&
                   !apiKey.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase);
        }
    }

    public async Task<Stream> SynthesizeSpeechAsync(string text, string? voice = null, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Yandex TTS API is not configured");

        var apiKey = _configuration["YandexTts:ApiKey"]!;
        var baseUrl = _configuration["YandexTts:BaseUrl"] ?? DefaultBaseUrl;
        var defaultVoice = _configuration["YandexTts:Voice"] ?? "marina";
        var lang = _configuration["YandexTts:Lang"] ?? "ru-RU";
        var role = _configuration["YandexTts:Role"];
        var speed = _configuration["YandexTts:Speed"];
        var folderId = _configuration["YandexTts:FolderId"];
        var sampleRate = int.TryParse(_configuration["YandexTts:SampleRateHertz"], out var sr) ? sr : 48000;

        var form = new List<KeyValuePair<string, string>>
        {
            new("text", text),
            new("lang", lang),
            new("voice", voice ?? defaultVoice),
            new("format", "lpcm"),
            new("sampleRateHertz", sampleRate.ToString()),
        };
        if (!string.IsNullOrWhiteSpace(speed)) form.Add(new("speed", speed));
        if (!string.IsNullOrWhiteSpace(role)) form.Add(new("emotion", role));
        if (!string.IsNullOrWhiteSpace(folderId)) form.Add(new("folderId", folderId));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/speech/v1/tts:synthesize")
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Api-Key {apiKey}");

        var response = await _httpClientFactory.CreateClient("YandexTts")
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Yandex TTS error: {StatusCode} - {Content}", response.StatusCode, errorContent);

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                throw new YandexTtsAuthenticationException("Yandex TTS authentication failed. Please check YandexTts:ApiKey.");

            if ((int)response.StatusCode == 429)
                throw new YandexTtsRateLimitException("Yandex TTS rate limit exceeded.");

            throw new YandexTtsException($"Yandex TTS API returned {response.StatusCode}: {errorContent}");
        }

        using var pcmStream = new MemoryStream();
        await response.Content.CopyToAsync(pcmStream, cancellationToken);
        var wavBytes = WrapLpcmInWav(pcmStream.ToArray(), sampleRate);

        _logger.LogInformation("Yandex TTS synthesized {TextLength} chars to {AudioBytes} bytes", text.Length, wavBytes.Length);
        return new MemoryStream(wavBytes);
    }

    private static byte[] WrapLpcmInWav(byte[] pcm, int sampleRate)
    {
        const short channels = 1;
        const short bitsPerSample = 16;
        var byteRate = sampleRate * channels * bitsPerSample / 8;
        var blockAlign = (short)(channels * bitsPerSample / 8);

        using var ms = new MemoryStream(44 + pcm.Length);
        using var writer = new BinaryWriter(ms);
        writer.Write("RIFF"u8);
        writer.Write(36 + pcm.Length);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(pcm.Length);
        writer.Write(pcm);
        return ms.ToArray();
    }
}
