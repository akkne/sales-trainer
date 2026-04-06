using System.Text;
using System.Text.Json;

namespace SalesTrainer.Api.Features.Voice;

public class ElevenLabsService : IElevenLabsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ElevenLabsService> _logger;

    private const string PlaceholderApiKey = "REPLACE_WITH_ELEVENLABS_API_KEY";
    private const string PlaceholderVoiceId = "REPLACE_WITH_VOICE_ID";

    public ElevenLabsService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ElevenLabsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured
    {
        get
        {
            var apiKey = _configuration["ElevenLabs:ApiKey"];
            var voiceId = _configuration["ElevenLabs:VoiceId"];
            return !string.IsNullOrWhiteSpace(apiKey) &&
                   apiKey != PlaceholderApiKey &&
                   !apiKey.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(voiceId) &&
                   voiceId != PlaceholderVoiceId &&
                   !voiceId.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase);
        }
    }

    public async Task<Stream> SynthesizeSpeechAsync(string text, string? voiceId = null, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("ElevenLabs API is not configured");
        }

        var apiKey = _configuration["ElevenLabs:ApiKey"]!;
        var baseUrl = _configuration["ElevenLabs:BaseUrl"] ?? "https://api.elevenlabs.io";
        var defaultVoiceId = _configuration["ElevenLabs:VoiceId"]!;
        var model = _configuration["ElevenLabs:Model"] ?? "eleven_flash_v2_5";
        var outputFormat = _configuration["ElevenLabs:OutputFormat"] ?? "mp3_44100_128";

        var effectiveVoiceId = voiceId ?? defaultVoiceId;

        var apiUrl = $"{baseUrl.TrimEnd('/')}/v1/text-to-speech/{effectiveVoiceId}/stream?output_format={outputFormat}";

        var httpClient = _httpClientFactory.CreateClient("ElevenLabs");
        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("xi-api-key", apiKey);

        var requestBody = new
        {
            text,
            model_id = model,
            voice_settings = new
            {
                stability = 0.5,
                similarity_boost = 0.75,
                style = 0.0,
                use_speaker_boost = true
            }
        };

        var jsonContent = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _logger.LogInformation("Calling ElevenLabs TTS API with voice {VoiceId}, model {Model}", effectiveVoiceId, model);

        var response = await httpClient.PostAsync(apiUrl, httpContent, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("ElevenLabs API error: {StatusCode} - {Content}", response.StatusCode, errorContent);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new ElevenLabsAuthException("ElevenLabs authentication failed. Please check API configuration.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                throw new ElevenLabsRateLimitException("ElevenLabs rate limit exceeded. Please try again later.");
            }

            throw new HttpRequestException($"ElevenLabs API returned {response.StatusCode}: {errorContent}");
        }

        _logger.LogDebug("ElevenLabs TTS response received, streaming audio");

        return await response.Content.ReadAsStreamAsync(ct);
    }
}

public class ElevenLabsAuthException(string message) : Exception(message);
public class ElevenLabsRateLimitException(string message) : Exception(message);
