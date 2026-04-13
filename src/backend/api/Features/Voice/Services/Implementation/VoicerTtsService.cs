using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SalesTrainer.Api.Features.Voice.Models;
using SalesTrainer.Api.Features.Voice.Services.Abstract;

namespace SalesTrainer.Api.Features.Voice.Services.Implementation;

internal sealed class VoicerTtsService : IVoicerTtsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VoicerTtsService> _logger;

    private const string PlaceholderApiKey = "REPLACE_WITH_VOICER_API_KEY";
    private const int MinimumTextLength = 500;

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

    public async Task<Stream> SynthesizeSpeechAsync(string text, string? voiceId = null, CancellationToken cancellationToken = default)
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
        var stability = double.TryParse(_configuration["VoicerTts:Stability"], out var stabilityValue) ? stabilityValue : 0.5;
        var similarityBoost = double.TryParse(_configuration["VoicerTts:SimilarityBoost"], out var similarityBoostValue) ? similarityBoostValue : 0.75;
        var speed = double.TryParse(_configuration["VoicerTts:Speed"], out var speedValue) ? speedValue : 1.0;
        var pollIntervalMilliseconds = int.TryParse(_configuration["VoicerTts:PollIntervalMs"], out var pollInterval) ? pollInterval : 500;
        var maximumPollAttempts = int.TryParse(_configuration["VoicerTts:MaxPollAttempts"], out var maxAttempts) ? maxAttempts : 120;

        var effectiveVoiceId = voiceId ?? defaultVoiceId;
        var paddedText = PadTextToMinimumLength(text);

        var httpClient = _httpClientFactory.CreateClient("VoicerTts");
        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        var taskId = await CreateTaskAsync(httpClient, baseUrl, paddedText, effectiveVoiceId, publicOwnerId, model, stability, similarityBoost, speed, cancellationToken);
        _logger.LogInformation("VoicerTts task created: {TaskId} for {TextLength} characters (padded from {OriginalLength})",
            taskId, paddedText.Length, text.Length);

        var status = await PollForCompletionAsync(httpClient, baseUrl, taskId, pollIntervalMilliseconds, maximumPollAttempts, cancellationToken);
        if (status != "ending")
        {
            throw new VoicerTtsException($"Task {taskId} ended with unexpected status: {status}");
        }

        var audioStream = await DownloadResultAsync(httpClient, baseUrl, taskId, cancellationToken);
        _logger.LogInformation("VoicerTts audio downloaded for task {TaskId}", taskId);

        return audioStream;
    }

    private static string PadTextToMinimumLength(string text)
    {
        if (text.Length >= MinimumTextLength)
        {
            return text;
        }

        var stringBuilder = new StringBuilder(text);
        while (stringBuilder.Length < MinimumTextLength)
        {
            stringBuilder.Append("... ");
            stringBuilder.Append(text);
        }
        return stringBuilder.ToString();
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
        CancellationToken cancellationToken)
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

        var jsonContent = JsonSerializer.Serialize(requestBody, JsonSerializerOptions);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{baseUrl.TrimEnd('/')}/tasks", httpContent, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("VoicerTts create task error: {StatusCode} - {Content}", response.StatusCode, errorContent);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new VoicerTtsAuthenticationException("VoicerTts authentication failed. Please check API configuration.");
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

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var createResponse = JsonSerializer.Deserialize<VoicerCreateTaskResponse>(responseJson, JsonSerializerOptions);

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
        int pollIntervalMilliseconds,
        int maximumAttempts,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < maximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await httpClient.GetAsync($"{baseUrl.TrimEnd('/')}/tasks/{taskId}/status", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("VoicerTts status check error: {StatusCode} - {Content}", response.StatusCode, errorContent);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new VoicerTtsException($"Task {taskId} not found");
                }

                throw new HttpRequestException($"VoicerTts status API returned {response.StatusCode}: {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var statusResponse = JsonSerializer.Deserialize<VoicerTaskStatusResponse>(responseJson, JsonSerializerOptions);

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
                    await Task.Delay(pollIntervalMilliseconds, cancellationToken);
                    break;

                default:
                    _logger.LogWarning("Unknown VoicerTts status: {Status}", status);
                    await Task.Delay(pollIntervalMilliseconds, cancellationToken);
                    break;
            }
        }

        throw new VoicerTtsTimeoutException($"Task {taskId} did not complete within {maximumAttempts * pollIntervalMilliseconds / 1000} seconds");
    }

    private async Task<Stream> DownloadResultAsync(
        HttpClient httpClient,
        string baseUrl,
        int taskId,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"{baseUrl.TrimEnd('/')}/tasks/{taskId}/result", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
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
        await response.Content.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;
        return memoryStream;
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
