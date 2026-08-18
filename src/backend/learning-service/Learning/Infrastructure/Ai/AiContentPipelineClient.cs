using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.Learning.Features.ContentGeneration.Models;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Infrastructure.Configuration;

namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// Phase 40.27. Calls ai-service's two internal content routes. Deliberately the same shape as
/// <see cref="AiEvaluationClient"/> — same named <c>HttpClient</c>, same internal-secret header, same
/// "a non-2xx is an <see cref="InvalidOperationException"/>" contract — so there is one story about
/// what a learning → ai failure looks like rather than two.
/// </summary>
internal sealed class AiContentPipelineClient(
    HttpClient httpClient,
    IOptions<AiServiceConfiguration> configurationOptions,
    ITenantContext tenantContext,
    ILogger<AiContentPipelineClient> logger) : IAiContentPipelineClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly AiServiceConfiguration _configuration = configurationOptions.Value;

    public Task<AiStructuredMaterial> StructureAsync(
        AiStructureMaterialRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return PostAsync<AiStructureMaterialRequest, AiStructuredMaterial>(
            _configuration.ContentStructurePath, request, cancellationToken);
    }

    public Task<AiGeneratedLesson> GenerateAsync(
        AiGenerateExercisesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return PostAsync<AiGenerateExercisesRequest, AiGeneratedLesson>(
            _configuration.ContentGeneratePath, request, cancellationToken);
    }

    public Task<AiRewrittenExercise> RewriteExerciseAsync(
        AiAdaptExerciseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return PostAsync<AiAdaptExerciseRequest, AiRewrittenExercise>(
            _configuration.ContentRewritePath, request, cancellationToken);
    }

    public Task<AiExerciseReview> ReviewExerciseAsync(
        AiAdaptExerciseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return PostAsync<AiAdaptExerciseRequest, AiExerciseReview>(
            _configuration.ContentReviewPath, request, cancellationToken);
    }

    /// <summary>
    /// Phase 40.33. Every route this client reaches is driven by a sweep, never by somebody waiting —
    /// so all four declare themselves <see cref="AiCallHeaders.BatchWorkload"/> and are the first work
    /// an organization running low on allowance loses.
    /// </summary>
    private async Task<TResult> PostAsync<TRequest, TResult>(
        string path,
        TRequest request,
        CancellationToken cancellationToken)
    {
        var requestUri = _configuration.BaseUrl.TrimEnd('/') + path;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(request, options: SerializerOptions),
        };

        AiCallHeaders.Apply(httpRequest, tenantContext, AiCallHeaders.BatchWorkload);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "AI content pipeline call {Path} returned {StatusCode}: {Body}",
                path, response.StatusCode, responseBody);
            throw new InvalidOperationException(
                $"AI content pipeline returned {(int)response.StatusCode} for '{path}'.");
        }

        var result = await response.Content.ReadFromJsonAsync<TResult>(SerializerOptions, cancellationToken);
        if (result is null)
        {
            throw new InvalidOperationException($"AI content pipeline returned an empty body for '{path}'.");
        }

        return result;
    }
}
