using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Infrastructure.Configuration;

namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// Grades one exercise answer by asking ai-service, which owns the provider key and the meter.
///
/// <para>
/// A non-2xx or an unreadable body is an <see cref="InvalidOperationException"/> and never a
/// half-filled result: the caller writes an attempt row from what comes back, so "the grade could not
/// be obtained" must not arrive looking like a score of zero. The provider's own response body is
/// logged and never put into the exception message.
/// </para>
///
/// <para>
/// Phase 40.33. A learner is waiting on their grade, so the call declares itself
/// <see cref="AiCallHeaders.InteractiveWorkload"/> and runs to the organization's full allowance
/// rather than stopping at the batch reserve.
/// </para>
/// </summary>
internal sealed class AiEvaluationClient : IAiEvaluationClient
{
    public const string HttpClientName = "AiService";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly AiServiceConfiguration _configuration;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<AiEvaluationClient> _logger;

    public AiEvaluationClient(
        HttpClient httpClient,
        IOptions<AiServiceConfiguration> configurationOptions,
        ITenantContext tenantContext,
        ILogger<AiEvaluationClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configurationOptions.Value;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<AiEvaluationResult> EvaluateAsync(
        AiEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestUri = _configuration.BaseUrl.TrimEnd('/') + _configuration.EvaluatePath;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(request, options: SerializerOptions),
        };

        AiCallHeaders.Apply(httpRequest, _tenantContext, AiCallHeaders.InteractiveWorkload);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

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
