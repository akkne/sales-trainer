using System.Net;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Company.Infrastructure.Configuration;

namespace Sellevate.Company.Infrastructure.Ai;

/// <summary>
/// Calls ai-service to score how ready a salesperson is for a given company, derived from the
/// feedback on their practice sessions.
///
/// <para>
/// The only client of the four with a third outcome: <see cref="HttpStatusCode.NoContent"/> means
/// ai-service fanned out over the supplied sessions and found no usable feedback. That is a
/// legitimate "not yet", not a failure, and it must stay distinguishable from one — the caller
/// negative-caches it, and treating it as an error would instead retry the whole fan-out on every
/// request.
/// </para>
/// </summary>
internal sealed class ReadinessAiClient : IReadinessAiClient
{
    private const string FailureLogTemplate = "AI readiness generation returned {StatusCode}: {Body}";
    private const string ServiceLabel = "AI readiness service";

    private readonly HttpClient _httpClient;
    private readonly AiServiceConfiguration _configuration;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ReadinessAiClient> _logger;

    public ReadinessAiClient(
        HttpClient httpClient,
        IOptions<AiServiceConfiguration> configurationOptions,
        ITenantContext tenantContext,
        ILogger<ReadinessAiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configurationOptions.Value;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<ReadinessAiResult?> GenerateReadinessAsync(
        ReadinessAiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await AiServiceCall.PostAsync(
            _httpClient,
            _tenantContext,
            AiServiceCall.BuildRequestUri(_configuration.BaseUrl, _configuration.ReadinessPath),
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;

        return await AiServiceCall.ReadResultAsync<ReadinessAiResult>(
            response, _logger, FailureLogTemplate, ServiceLabel, cancellationToken);
    }
}
