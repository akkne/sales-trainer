using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SalesTrainer.Api.Features.Voice.Models;
using SalesTrainer.Api.Features.Voice.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Configuration;

namespace SalesTrainer.Api.Features.Voice.Services.Implementation;

internal sealed class GoogleTtsService : IGoogleTtsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<GoogleTtsConfiguration> _googleTtsOptions;
    private readonly ILogger<GoogleTtsService> _logger;

    private const string PlaceholderApiKey = "REPLACE_WITH_GOOGLE_API_KEY";

    public GoogleTtsService(
        IHttpClientFactory httpClientFactory,
        IOptions<GoogleTtsConfiguration> googleTtsOptions,
        ILogger<GoogleTtsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _googleTtsOptions = googleTtsOptions;
        _logger = logger;
    }

    public bool IsConfigured
    {
        get
        {
            var apiKey = _googleTtsOptions.Value.ApiKey;
            return !string.IsNullOrWhiteSpace(apiKey) &&
                   apiKey != PlaceholderApiKey &&
                   !apiKey.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase);
        }
    }

    public async Task<Stream> SynthesizeSpeechAsync(string text, string? voiceName = null, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Google TTS API is not configured");

        var configuration = _googleTtsOptions.Value;

        var requestBody = new GoogleTtsSynthesisRequest
        {
            Input = new GoogleTtsSynthesisInput { Text = text },
            Voice = new GoogleTtsSynthesisVoice
            {
                LanguageCode = configuration.LanguageCode,
                Name = voiceName ?? configuration.VoiceName
            },
            AudioConfig = new GoogleTtsAudioConfiguration
            {
                AudioEncoding = "MP3",
                SpeakingRate = configuration.SpeakingRate,
                Pitch = configuration.Pitch
            }
        };

        var httpContent = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClientFactory.CreateClient("GoogleTts")
            .PostAsync($"https://texttospeech.googleapis.com/v1/text:synthesize?key={configuration.ApiKey}", httpContent, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Google TTS error: {StatusCode} - {Content}", response.StatusCode, errorContent);

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                throw new GoogleTtsAuthenticationException("Google TTS authentication failed. Please check API key.");

            if ((int)response.StatusCode == 429)
                throw new GoogleTtsRateLimitException("Google TTS rate limit exceeded.");

            throw new GoogleTtsException($"Google TTS API returned {response.StatusCode}: {errorContent}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var ttsResponse = JsonSerializer.Deserialize<GoogleTtsSynthesisResponse>(responseJson, JsonOptions);

        if (string.IsNullOrEmpty(ttsResponse?.AudioContent))
            throw new GoogleTtsException("No audio content in Google TTS response");

        var audioBytes = Convert.FromBase64String(ttsResponse.AudioContent);
        _logger.LogInformation("Google TTS synthesized {TextLength} chars to {AudioBytes} bytes", text.Length, audioBytes.Length);

        return new MemoryStream(audioBytes);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class TtsRequest
    {
        public TtsInput Input { get; set; } = null!;
        public TtsVoice Voice { get; set; } = null!;
        public TtsAudioConfig AudioConfig { get; set; } = null!;
    }

    private sealed class TtsInput
    {
        public string Text { get; set; } = null!;
    }

    private sealed class TtsVoice
    {
        public string LanguageCode { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

    private sealed class TtsAudioConfig
    {
        public string AudioEncoding { get; set; } = null!;
        public double SpeakingRate { get; set; }
        public double Pitch { get; set; }
    }

    private sealed class TtsResponse
    {
        public string? AudioContent { get; set; }
    }
}
