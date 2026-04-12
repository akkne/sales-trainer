using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SalesTrainer.Api.Features.Voice;

public class GoogleTtsService : IGoogleTtsService
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

    public async Task<Stream> SynthesizeSpeechAsync(string text, string? voiceName = null, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Google TTS API is not configured");
        }

        var apiKey = _configuration["GoogleTts:ApiKey"]!;
        var defaultVoiceName = _configuration["GoogleTts:VoiceName"] ?? "ru-RU-Wavenet-A";
        var languageCode = _configuration["GoogleTts:LanguageCode"] ?? "ru-RU";
        var speakingRate = double.TryParse(_configuration["GoogleTts:SpeakingRate"], out var sr) ? sr : 1.0;
        var pitch = double.TryParse(_configuration["GoogleTts:Pitch"], out var p) ? p : 0.0;

        var effectiveVoiceName = voiceName ?? defaultVoiceName;

        var httpClient = _httpClientFactory.CreateClient("GoogleTts");

        var requestBody = new GoogleTtsRequest
        {
            Input = new GoogleTtsInput { Text = text },
            Voice = new GoogleTtsVoice
            {
                LanguageCode = languageCode,
                Name = effectiveVoiceName
            },
            AudioConfig = new GoogleTtsAudioConfig
            {
                AudioEncoding = "MP3",
                SpeakingRate = speakingRate,
                Pitch = pitch
            }
        };

        var jsonContent = JsonSerializer.Serialize(requestBody, JsonOptions);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var url = $"https://texttospeech.googleapis.com/v1/text:synthesize?key={apiKey}";
        var response = await httpClient.PostAsync(url, httpContent, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Google TTS error: {StatusCode} - {Content}", response.StatusCode, errorContent);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                throw new GoogleTtsAuthException("Google TTS authentication failed. Please check API key.");
            }

            if ((int)response.StatusCode == 429)
            {
                throw new GoogleTtsRateLimitException("Google TTS rate limit exceeded.");
            }

            throw new GoogleTtsException($"Google TTS API returned {response.StatusCode}: {errorContent}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var ttsResponse = JsonSerializer.Deserialize<GoogleTtsResponse>(responseJson, JsonOptions);

        if (string.IsNullOrEmpty(ttsResponse?.AudioContent))
        {
            throw new GoogleTtsException("No audio content in Google TTS response");
        }

        var audioBytes = Convert.FromBase64String(ttsResponse.AudioContent);
        _logger.LogInformation("Google TTS synthesized {TextLength} chars to {AudioBytes} bytes",
            text.Length, audioBytes.Length);

        return new MemoryStream(audioBytes);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

#region DTOs

public class GoogleTtsRequest
{
    public GoogleTtsInput Input { get; set; } = null!;
    public GoogleTtsVoice Voice { get; set; } = null!;
    public GoogleTtsAudioConfig AudioConfig { get; set; } = null!;
}

public class GoogleTtsInput
{
    public string Text { get; set; } = null!;
}

public class GoogleTtsVoice
{
    public string LanguageCode { get; set; } = null!;
    public string Name { get; set; } = null!;
}

public class GoogleTtsAudioConfig
{
    public string AudioEncoding { get; set; } = null!;
    public double SpeakingRate { get; set; }
    public double Pitch { get; set; }
}

public class GoogleTtsResponse
{
    public string? AudioContent { get; set; }
}

#endregion

#region Exceptions

public class GoogleTtsException(string message) : Exception(message);
public class GoogleTtsAuthException(string message) : GoogleTtsException(message);
public class GoogleTtsRateLimitException(string message) : GoogleTtsException(message);

#endregion
