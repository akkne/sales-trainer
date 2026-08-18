using Microsoft.Extensions.Options;
using Sellevate.Ai.Common.Constants;
using Sellevate.Ai.Features.Voice.Models;
using Sellevate.Ai.Features.Quotas.Models;
using Sellevate.Ai.Features.Quotas.Services.Abstract;
using Sellevate.Ai.Features.Voice.Services.Abstract;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Voice.Services.Implementation;

/// <summary>
/// Speech synthesis against Yandex SpeechKit. Returns a WAV stream: the provider is asked for raw
/// LPCM and the RIFF header is written here, so the sample rate in the header can never disagree
/// with the rate the audio was sampled at.
///
/// <para>
/// <b>Synthesis is metered here rather than in the router.</b> Yandex bills per character, and
/// <see cref="CachingTtsRouter"/> answers short repeated phrases from memory with no provider call.
/// A cache hit costs nothing, so it has to be recorded as nothing — which only holds if the charge
/// sits at the point that actually spends money (Phase 40.33).
/// </para>
///
/// <para>
/// <see cref="IsConfigured"/> is the feature switch the whole voice surface reads. It is false for a
/// blank key and for a placeholder one, so an environment that never had the secret injected
/// degrades to text rather than issuing an authenticated-looking request that cannot succeed.
/// </para>
/// </summary>
internal sealed class YandexTtsService : IYandexTtsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<YandexTtsConfiguration> _yandexTtsOptions;
    private readonly IAiSpendMeter _spendMeter;
    private readonly ILogger<YandexTtsService> _logger;

    public YandexTtsService(
        IHttpClientFactory httpClientFactory,
        IOptions<YandexTtsConfiguration> yandexTtsOptions,
        IAiSpendMeter spendMeter,
        ILogger<YandexTtsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _yandexTtsOptions = yandexTtsOptions;
        _spendMeter = spendMeter;
        _logger = logger;
    }

    public bool IsConfigured => AiSecretPlaceholders.IsRealSecret(_yandexTtsOptions.Value.ApiKey);

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
            new("format", configuration.AudioFormat),
            new("sampleRateHertz", configuration.SampleRateHertz.ToString()),
        };
        if (!string.IsNullOrWhiteSpace(configuration.Speed)) form.Add(new("speed", configuration.Speed));
        if (!string.IsNullOrWhiteSpace(configuration.Role)) form.Add(new("emotion", configuration.Role));
        if (!string.IsNullOrWhiteSpace(configuration.FolderId)) form.Add(new("folderId", configuration.FolderId));

        var synthesizeUrl = configuration.BaseUrl.TrimEnd('/') + configuration.SynthesizePath;
        using var request = new HttpRequestMessage(HttpMethod.Post, synthesizeUrl)
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.TryAddWithoutValidation(
            AiProviderHttpConstants.AuthorizationHeaderName,
            $"{AiProviderHttpConstants.YandexApiKeyScheme} {configuration.ApiKey}");

        var response = await _httpClientFactory.CreateClient(AiProviderHttpConstants.YandexTtsClientName)
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Yandex TTS error: {StatusCode} - {Content}", response.StatusCode, errorContent);

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                throw new YandexTtsAuthenticationException("Yandex TTS authentication failed. Please check YandexTts:ApiKey.");

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                throw new YandexTtsRateLimitException("Yandex TTS rate limit exceeded.");

            throw new YandexTtsException($"Yandex TTS API returned {response.StatusCode}: {errorContent}");
        }

        using var pcmStream = new MemoryStream();
        await response.Content.CopyToAsync(pcmStream, cancellationToken);
        var wavBytes = WrapLpcmInWav(pcmStream.ToArray(), configuration.SampleRateHertz);

        _logger.LogInformation("Yandex TTS synthesized {TextLength} chars to {AudioBytes} bytes", text.Length, wavBytes.Length);

        await _spendMeter.RecordSpeechUsageAsync(
            AiUsageKinds.Tts, configuration.MeteredModelName, text.Length, cancellationToken);

        return new MemoryStream(wavBytes);
    }

    /// <summary>
    /// Writes the 44-byte canonical RIFF/WAVE header in front of raw little-endian PCM so a browser
    /// can play the bytes without knowing how they were sampled.
    /// </summary>
    private static byte[] WrapLpcmInWav(byte[] pcm, int sampleRate)
    {
        const short channels = 1;
        const short bitsPerSample = 16;
        const short pcmFormatTag = 1;
        const int bitsPerByte = 8;
        const int formatChunkSize = 16;
        const int headerBytes = 44;
        const int bytesBeforeRiffSizeField = 8;

        var byteRate = sampleRate * channels * bitsPerSample / bitsPerByte;
        var blockAlign = (short)(channels * bitsPerSample / bitsPerByte);

        using var memoryStream = new MemoryStream(headerBytes + pcm.Length);
        using var writer = new BinaryWriter(memoryStream);
        writer.Write("RIFF"u8);
        writer.Write(headerBytes - bytesBeforeRiffSizeField + pcm.Length);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(formatChunkSize);
        writer.Write(pcmFormatTag);
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
