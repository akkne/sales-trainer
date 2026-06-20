using Microsoft.Extensions.Options;

namespace Sellevate.Learning.Infrastructure.Ai;

internal sealed class YandexTtsService : IYandexTtsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<YandexTtsConfiguration> _yandexTtsOptions;
    private readonly ILogger<YandexTtsService> _logger;

    private const string PlaceholderApiKey = "REPLACE_WITH_YANDEX_API_KEY";

    public YandexTtsService(
        IHttpClientFactory httpClientFactory,
        IOptions<YandexTtsConfiguration> yandexTtsOptions,
        ILogger<YandexTtsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _yandexTtsOptions = yandexTtsOptions;
        _logger = logger;
    }

    public bool IsConfigured
    {
        get
        {
            var apiKey = _yandexTtsOptions.Value.ApiKey;
            return !string.IsNullOrWhiteSpace(apiKey) &&
                   apiKey != PlaceholderApiKey &&
                   !apiKey.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase);
        }
    }

    public async Task<Stream> SynthesizeSpeechAsync(string text, string? voice = null, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Yandex TTS API is not configured");

        var configuration = _yandexTtsOptions.Value;

        var form = new List<KeyValuePair<string, string>>
        {
            new("text", text),
            new("lang", configuration.Lang),
            new("voice", voice ?? configuration.Voice),
            new("format", "lpcm"),
            new("sampleRateHertz", configuration.SampleRateHertz.ToString()),
        };
        if (!string.IsNullOrWhiteSpace(configuration.Speed)) form.Add(new("speed", configuration.Speed));
        if (!string.IsNullOrWhiteSpace(configuration.Role)) form.Add(new("emotion", configuration.Role));
        if (!string.IsNullOrWhiteSpace(configuration.FolderId)) form.Add(new("folderId", configuration.FolderId));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{configuration.BaseUrl.TrimEnd('/')}/speech/v1/tts:synthesize")
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Api-Key {configuration.ApiKey}");

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
        var wavBytes = WrapLpcmInWav(pcmStream.ToArray(), configuration.SampleRateHertz);

        _logger.LogInformation("Yandex TTS synthesized {TextLength} chars to {AudioBytes} bytes", text.Length, wavBytes.Length);
        return new MemoryStream(wavBytes);
    }

    private static byte[] WrapLpcmInWav(byte[] pcm, int sampleRate)
    {
        const short channels = 1;
        const short bitsPerSample = 16;
        var byteRate = sampleRate * channels * bitsPerSample / 8;
        var blockAlign = (short)(channels * bitsPerSample / 8);

        using var memoryStream = new MemoryStream(44 + pcm.Length);
        using var writer = new BinaryWriter(memoryStream);
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
        return memoryStream.ToArray();
    }
}
