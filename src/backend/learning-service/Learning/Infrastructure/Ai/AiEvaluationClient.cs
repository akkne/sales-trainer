using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.Learning.Infrastructure.Configuration;

namespace Sellevate.Learning.Infrastructure.Ai;

internal sealed class AiEvaluationClient : IAiEvaluationClient
{
    public const string HttpClientName = "AiService";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly AiServiceConfiguration _configuration;
    private readonly ILogger<AiEvaluationClient> _logger;

    public AiEvaluationClient(
        HttpClient httpClient,
        IOptions<AiServiceConfiguration> configurationOptions,
        ILogger<AiEvaluationClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configurationOptions.Value;
        _logger = logger;
    }

    public async Task<AiEvaluationResult> EvaluateAsync(
        AiEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestUri = _configuration.BaseUrl.TrimEnd('/') + _configuration.EvaluatePath;

        using var response = await _httpClient.PostAsJsonAsync(
            requestUri, request, SerializerOptions, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "AI evaluation for {ExerciseType} returned {StatusCode}: {Body}",
                request.ExerciseType, response.StatusCode, responseBody);
            throw new InvalidOperationException(
                $"AI evaluation service returned {(int)response.StatusCode} for exercise type '{request.ExerciseType}'.");
        }

        var result = await response.Content.ReadFromJsonAsync<AiEvaluationResult>(
            SerializerOptions, cancellationToken);

        if (result is null)
        {
            throw new InvalidOperationException(
                $"AI evaluation service returned an empty body for exercise type '{request.ExerciseType}'.");
        }

        return result;
    }
}
