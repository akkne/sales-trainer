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
            throw new InvalidOperationException("VoicerTts API is not configured");

        var apiKey = _configuration["VoicerTts:ApiKey"]!;
        var baseUrl = _configuration["VoicerTts:BaseUrl"] ?? "https://voiceapi.csv666.ru";
        var defaultVoiceId = _configuration["VoicerTts:VoiceId"] ?? "21m00Tcm4TlvDq8ikWAM";
        var publicOwnerId = _configuration["VoicerTts:PublicOwnerId"] ?? "default";
        var model = _configuration["VoicerTts:Model"] ?? "eleven_multilingual_v2";
        var stability = double.TryParse(_configuration["VoicerTts:Stability"], out var stab) ? stab : 0.5;
        var similarityBoost = double.TryParse(_configuration["VoicerTts:SimilarityBoost"], out var sim) ? sim : 0.75;
        var speed = double.TryParse(_configuration["VoicerTts:Speed"], out var spd) ? spd : 1.0;
        var pollIntervalMs = int.TryParse(_configuration["VoicerTts:PollIntervalMs"], out var pi) ? pi : 500;
        var maxPollAttempts = int.TryParse(_configuration["VoicerTts:MaxPollAttempts"], out var ma) ? ma : 120;

        var httpClient = _httpClientFactory.CreateClient("VoicerTts");
        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        var taskId = await CreateTaskAsync(httpClient, baseUrl, text, voiceId ?? defaultVoiceId, publicOwnerId, model, stability, similarityBoost, speed, cancellationToken);
        _logger.LogInformation("VoicerTts task created: {TaskId} for {TextLength} characters", taskId, text.Length);

        var status = await PollForCompletionAsync(httpClient, baseUrl, taskId, pollIntervalMs, maxPollAttempts, cancellationToken);
        if (status != "ending")
            throw new VoicerTtsException($"Task {taskId} ended with unexpected status: {status}");

        var audioStream = await DownloadResultAsync(httpClient, baseUrl, taskId, cancellationToken);
        _logger.LogInformation("VoicerTts audio downloaded for task {TaskId}", taskId);

        return audioStream;
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

        var httpContent = new StringContent(JsonSerializer.Serialize(requestBody, JsonSerializerOptions), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync($"{baseUrl.TrimEnd('/')}/tasks", httpContent, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("VoicerTts create task error: {StatusCode} - {Content}", response.StatusCode, errorContent);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new VoicerTtsAuthenticationException("VoicerTts authentication failed. Please check API configuration.");

            if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                throw new VoicerTtsInsufficientFundsException("VoicerTts insufficient funds.");

            if ((int)response.StatusCode == 429)
                throw new VoicerTtsRateLimitException("VoicerTts rate limit exceeded. Maximum 5 concurrent tasks.");

            throw new HttpRequestException($"VoicerTts API returned {response.StatusCode}: {errorContent}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var createResponse = JsonSerializer.Deserialize<VoicerCreateTaskResponse>(responseJson, JsonSerializerOptions);

        if (createResponse?.TaskId == null)
            throw new VoicerTtsException("Failed to parse task_id from VoicerTts response");

        return createResponse.TaskId.Value;
    }

    private async Task<string> PollForCompletionAsync(
        HttpClient httpClient,
        string baseUrl,
        int taskId,
        int pollIntervalMs,
        int maxAttempts,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = await httpClient.GetAsync($"{baseUrl.TrimEnd('/')}/tasks/{taskId}/status", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("VoicerTts status check error: {StatusCode} - {Content}", response.StatusCode, errorContent);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    throw new VoicerTtsException($"Task {taskId} not found");

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

                default:
                    await Task.Delay(pollIntervalMs, cancellationToken);
                    break;
            }
        }

        throw new VoicerTtsTimeoutException($"Task {taskId} did not complete within {maxAttempts * pollIntervalMs / 1000} seconds");
    }

    private async Task<Stream> DownloadResultAsync(HttpClient httpClient, string baseUrl, int taskId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"{baseUrl.TrimEnd('/')}/tasks/{taskId}/result", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("VoicerTts download error: {StatusCode} - {Content}", response.StatusCode, errorContent);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new VoicerTtsException($"Result for task {taskId} not found");

            if (response.StatusCode == System.Net.HttpStatusCode.Gone)
                throw new VoicerTtsException($"Result for task {taskId} is no longer available");

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
