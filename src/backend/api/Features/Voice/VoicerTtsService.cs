using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SalesTrainer.Api.Features.Voice;

public class VoicerTtsService : IVoicerTtsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VoicerTtsService> _logger;

    private const string PlaceholderApiKey = "REPLACE_WITH_VOICER_API_KEY";

    public VoicerTtsService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<VoicerTtsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured
    {
        get
        {
            var apiKey = _configuration["VoicerTts:ApiKey"];
            return !string.IsNullOrWhiteSpace(apiKey) &&
                   apiKey != PlaceholderApiKey &&
                   !apiKey.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase);
        }
    }

    private const int MinTextLength = 500;

    public async Task<Stream> SynthesizeSpeechAsync(string text, string? voiceId = null, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("VoicerTts API is not configured");
        }

        var apiKey = _configuration["VoicerTts:ApiKey"]!;
        var baseUrl = _configuration["VoicerTts:BaseUrl"] ?? "https://voiceapi.csv666.ru";
        var defaultVoiceId = _configuration["VoicerTts:VoiceId"] ?? "21m00Tcm4TlvDq8ikWAM";
        var publicOwnerId = _configuration["VoicerTts:PublicOwnerId"] ?? "default";
        var model = _configuration["VoicerTts:Model"] ?? "eleven_multilingual_v2";
        var stability = double.TryParse(_configuration["VoicerTts:Stability"], out var s) ? s : 0.5;
        var similarityBoost = double.TryParse(_configuration["VoicerTts:SimilarityBoost"], out var sb) ? sb : 0.75;
        var speed = double.TryParse(_configuration["VoicerTts:Speed"], out var sp) ? sp : 1.0;
        var pollIntervalMs = int.TryParse(_configuration["VoicerTts:PollIntervalMs"], out var pi) ? pi : 500;
        var maxPollAttempts = int.TryParse(_configuration["VoicerTts:MaxPollAttempts"], out var mpa) ? mpa : 120;

        var effectiveVoiceId = voiceId ?? defaultVoiceId;

        // Pad short text with SSML silence to meet 500 char minimum
        var paddedText = PadTextToMinLength(text);

        var httpClient = _httpClientFactory.CreateClient("VoicerTts");
        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        // Step 1: Create TTS task
        var taskId = await CreateTaskAsync(httpClient, baseUrl, paddedText, effectiveVoiceId, publicOwnerId, model, stability, similarityBoost, speed, ct);
        _logger.LogInformation("VoicerTts task created: {TaskId} for {TextLength} characters (padded from {OriginalLength})",
            taskId, paddedText.Length, text.Length);

        // Step 2: Poll for completion
        var status = await PollForCompletionAsync(httpClient, baseUrl, taskId, pollIntervalMs, maxPollAttempts, ct);
        if (status != "ending")
        {
            throw new VoicerTtsException($"Task {taskId} ended with unexpected status: {status}");
        }

        // Step 3: Download result
        var audioStream = await DownloadResultAsync(httpClient, baseUrl, taskId, ct);
        _logger.LogInformation("VoicerTts audio downloaded for task {TaskId}", taskId);

        return audioStream;
    }

    /// <summary>
    /// Pads text to meet the VoicerTts minimum 500 character requirement.
    /// Repeats the original text with natural pauses to reach minimum length.
    /// </summary>
    private static string PadTextToMinLength(string text)
    {
        if (text.Length >= MinTextLength)
        {
            return text;
        }

        // Repeat text with pause markers until we reach minimum length
        var sb = new StringBuilder(text);
        while (sb.Length < MinTextLength)
        {
            sb.Append("... ");
            sb.Append(text);
        }
        return sb.ToString();
    }

    private async Task<int> CreateTaskAsync(
        HttpClient httpClient,
        string baseUrl,
        string text,
        string voiceId,
        string publicOwnerId,
        string model,
        double stability,
        double similarityBoost,
        double speed,
        CancellationToken ct)
    {
        var requestBody = new VoicerCreateTaskRequest
        {
            Text = text,
            Template = new VoicerTemplate
            {
                VoiceId = voiceId,
                PublicOwnerId = publicOwnerId,
                ModelId = model,
                VoiceSettings = new VoicerVoiceSettings
                {
                    Stability = stability,
                    SimilarityBoost = similarityBoost,
                    UseSpeakerBoost = true,
                    Speed = speed
                }
            }
        };

        var jsonContent = JsonSerializer.Serialize(requestBody, JsonOptions);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{baseUrl.TrimEnd('/')}/tasks", httpContent, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("VoicerTts create task error: {StatusCode} - {Content}", response.StatusCode, errorContent);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new VoicerTtsAuthException("VoicerTts authentication failed. Please check API configuration.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
            {
                throw new VoicerTtsInsufficientFundsException("VoicerTts insufficient funds.");
            }

            if ((int)response.StatusCode == 429)
            {
                throw new VoicerTtsRateLimitException("VoicerTts rate limit exceeded. Maximum 5 concurrent tasks.");
            }

            throw new HttpRequestException($"VoicerTts API returned {response.StatusCode}: {errorContent}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var createResponse = JsonSerializer.Deserialize<VoicerCreateTaskResponse>(responseJson, JsonOptions);

        if (createResponse?.TaskId == null)
        {
            throw new VoicerTtsException("Failed to parse task_id from VoicerTts response");
        }

        return createResponse.TaskId.Value;
    }

    private async Task<string> PollForCompletionAsync(
        HttpClient httpClient,
        string baseUrl,
        int taskId,
        int pollIntervalMs,
        int maxAttempts,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            var response = await httpClient.GetAsync($"{baseUrl.TrimEnd('/')}/tasks/{taskId}/status", ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("VoicerTts status check error: {StatusCode} - {Content}", response.StatusCode, errorContent);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new VoicerTtsException($"Task {taskId} not found");
                }

                throw new HttpRequestException($"VoicerTts status API returned {response.StatusCode}: {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var statusResponse = JsonSerializer.Deserialize<VoicerTaskStatusResponse>(responseJson, JsonOptions);

            var status = statusResponse?.Status;
            _logger.LogDebug("VoicerTts task {TaskId} status: {Status}", taskId, status);

            switch (status)
            {
                case "ending":
                case "ending_processed":
                    return "ending";

                case "error":
                case "error_handled":
                    throw new VoicerTtsException($"Task {taskId} failed with error status: {statusResponse?.StatusLabel}");

                case "waiting":
                case "processing":
                    await Task.Delay(pollIntervalMs, ct);
                    break;

                default:
                    _logger.LogWarning("Unknown VoicerTts status: {Status}", status);
                    await Task.Delay(pollIntervalMs, ct);
                    break;
            }
        }

        throw new VoicerTtsTimeoutException($"Task {taskId} did not complete within {maxAttempts * pollIntervalMs / 1000} seconds");
    }

    private async Task<Stream> DownloadResultAsync(
        HttpClient httpClient,
        string baseUrl,
        int taskId,
        CancellationToken ct)
    {
        var response = await httpClient.GetAsync($"{baseUrl.TrimEnd('/')}/tasks/{taskId}/result", ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("VoicerTts download error: {StatusCode} - {Content}", response.StatusCode, errorContent);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new VoicerTtsException($"Result for task {taskId} not found");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Gone)
            {
                throw new VoicerTtsException($"Result for task {taskId} is no longer available");
            }

            throw new HttpRequestException($"VoicerTts result API returned {response.StatusCode}: {errorContent}");
        }

        var memoryStream = new MemoryStream();
        await response.Content.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;
        return memoryStream;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

#region DTOs

public class VoicerCreateTaskRequest
{
    public string Text { get; set; } = null!;
    public string? TemplateUuid { get; set; }
    public VoicerTemplate? Template { get; set; }
}

public class VoicerTemplate
{
    public string VoiceId { get; set; } = null!;
    public string PublicOwnerId { get; set; } = null!;
    public string? ModelId { get; set; }
    public VoicerVoiceSettings? VoiceSettings { get; set; }
}

public class VoicerVoiceSettings
{
    public double Stability { get; set; }
    public double SimilarityBoost { get; set; }
    public bool UseSpeakerBoost { get; set; }
    public double Speed { get; set; }
}

public class VoicerCreateTaskResponse
{
    public int? TaskId { get; set; }
    public string? Message { get; set; }
}

public class VoicerTaskStatusResponse
{
    public int? TaskId { get; set; }
    public string? Status { get; set; }
    public string? StatusLabel { get; set; }
    public string? CreatedAt { get; set; }
}

#endregion

#region Exceptions

public class VoicerTtsException(string message) : Exception(message);
public class VoicerTtsAuthException(string message) : VoicerTtsException(message);
public class VoicerTtsRateLimitException(string message) : VoicerTtsException(message);
public class VoicerTtsInsufficientFundsException(string message) : VoicerTtsException(message);
public class VoicerTtsTimeoutException(string message) : VoicerTtsException(message);

#endregion
