using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SalesTrainer.Api.Features.Voice.Models;
using SalesTrainer.Api.Features.Voice.Services.Abstract;

namespace SalesTrainer.Api.Features.Voice.Services.Implementation;

internal sealed class GoogleTtsService : IGoogleTtsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleTtsService> _logger;

    private const string PlaceholderApiKey = "REPLACE_WITH_GOOGLE_API_KEY";

    public GoogleTtsService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GoogleTtsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured
    {
        get
        {
            var apiKey = _configuration["GoogleTts:ApiKey"];
            return !string.IsNullOrWhiteSpace(apiKey) &&
                   apiKey != PlaceholderApiKey &&
                   !apiKey.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase);
        }
    }

    public async Task<Stream> SynthesizeSpeechAsync(string text, string? voiceName = null, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Google TTS API is not configured");

        var apiKey = _configuration["GoogleTts:ApiKey"]!;
        var defaultVoiceName = _configuration["GoogleTts:VoiceName"] ?? "ru-RU-Wavenet-A";
        var languageCode = _configuration["GoogleTts:LanguageCode"] ?? "ru-RU";
        var speakingRate = double.TryParse(_configuration["GoogleTts:SpeakingRate"], out var sr) ? sr : 1.0;
        var pitch = double.TryParse(_configuration["GoogleTts:Pitch"], out var p) ? p : 0.0;

        var requestBody = new TtsRequest
        {
            Input = new TtsInput { Text = text },
            Voice = new TtsVoice { LanguageCode = languageCode, Name = voiceName ?? defaultVoiceName },
            AudioConfig = new TtsAudioConfig { AudioEncoding = "MP3", SpeakingRate = speakingRate, Pitch = pitch }
        };

        var httpContent = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClientFactory.CreateClient("GoogleTts")
            .PostAsync($"https://texttospeech.googleapis.com/v1/text:synthesize?key={apiKey}", httpContent, cancellationToken);

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
        var ttsResponse = JsonSerializer.Deserialize<TtsResponse>(responseJson, JsonOptions);

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
