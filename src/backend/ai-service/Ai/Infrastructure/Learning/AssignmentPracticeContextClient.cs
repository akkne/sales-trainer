using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.BuildingBlocks.Identity;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Ai.Infrastructure.Learning;

/// <summary>
/// Phase 40.23. Reads an assignment's practice context out of learning-service.
///
/// <para>
/// The organization travels as the gateway-shaped <c>X-Organization-Id</c> header taken from
/// <see cref="ITenantContext"/> — never from anything the learner's request said — and the call is
/// authenticated with the same internal shared secret ai-service's own internal routes expect from
/// their callers.
/// </para>
///
/// <para>
/// <b>It degrades rather than fails.</b> Practising is the product; an assignment's persona is an
/// improvement to it, and refusing to start a conversation because learning-service is unreachable would
/// take the whole feature down with the one that decorates it. Every failure other than the caller's own
/// cancellation returns null and logs a warning.
/// </para>
/// </summary>
internal sealed class AssignmentPracticeContextClient : IAssignmentPracticeContextClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ITenantContext _tenantContext;
    private readonly LearningServiceConfiguration _configuration;
    private readonly ILogger<AssignmentPracticeContextClient> _logger;

    public AssignmentPracticeContextClient(
        HttpClient httpClient,
        ITenantContext tenantContext,
        IOptions<LearningServiceConfiguration> configurationOptions,
        ILogger<AssignmentPracticeContextClient> logger)
    {
        _httpClient = httpClient;
        _tenantContext = tenantContext;
        _configuration = configurationOptions.Value;
        _logger = logger;
    }

    public async Task<AssignmentPracticeContext?> GetPracticeContextAsync(
        Guid userId,
        string dialogModeKey,
        CancellationToken cancellationToken = default)
    {
        var organizationId = _tenantContext.OrganizationId;
        if (organizationId is null || userId == Guid.Empty || string.IsNullOrWhiteSpace(dialogModeKey))
        {
            return null;
        }

        var requestUri = _configuration.BaseUrl.TrimEnd('/')
                         + _configuration.PracticeContextPath
                         + $"?userId={userId}&modeKey={Uri.EscapeDataString(dialogModeKey)}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add(IdentityHeaders.OrganizationId, organizationId.Value.ToString());

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Learning-service returned {StatusCode} for the assignment practice context of mode {ModeKey}; "
                    + "the conversation starts without one.",
                    response.StatusCode, dialogModeKey);

                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<PracticeContextResponse>(
                SerializerOptions, cancellationToken);

            if (payload is null || payload.AssignmentId == Guid.Empty)
            {
                return null;
            }

            return new AssignmentPracticeContext
            {
                AssignmentId = payload.AssignmentId,
                Title = payload.Title ?? string.Empty,
                Goal = payload.Goal,
                PersonaName = payload.PersonaName,
                PersonaPosition = payload.PersonaPosition,
                PersonaPersonality = payload.PersonaPersonality,
                PersonaDifficulty = payload.PersonaDifficulty,
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "The assignment practice context for mode {ModeKey} could not be read; "
                + "the conversation starts without one.",
                dialogModeKey);

            return null;
        }
    }

    /// <summary>
    /// A local copy of learning-service's <c>AssignmentPracticeContextDto</c> — services agree on a
    /// wire shape, not on a type, exactly as the Kafka contracts do.
    /// </summary>
    private sealed record PracticeContextResponse(
        Guid AssignmentId,
        string? Title,
        string? Goal,
        string? PersonaName,
        string? PersonaPosition,
        string? PersonaPersonality,
        string? PersonaDifficulty);
}
