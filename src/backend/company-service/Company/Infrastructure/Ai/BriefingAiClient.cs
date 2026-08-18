using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Company.Infrastructure.Configuration;

namespace Sellevate.Company.Infrastructure.Ai;

/// <summary>
/// Calls ai-service to compose a pre-call briefing from a company's description, its latest practice
/// goal and its recent call log. Stateless: the caller owns caching the result on the company row.
///
/// <para>
/// Any failure — refusal, transport error, empty body — surfaces as an exception rather than a
/// degraded briefing, because a briefing the salesperson cannot tell apart from a real one is worse
/// than no briefing.
/// </para>
/// </summary>
internal sealed class BriefingAiClient : IBriefingAiClient
{
    private const string FailureLogTemplate = "AI briefing generation returned {StatusCode}: {Body}";
    private const string ServiceLabel = "AI briefing service";

    private readonly HttpClient _httpClient;
    private readonly AiServiceConfiguration _configuration;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<BriefingAiClient> _logger;

    public BriefingAiClient(
        HttpClient httpClient,
        IOptions<AiServiceConfiguration> configurationOptions,
        ITenantContext tenantContext,
        ILogger<BriefingAiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configurationOptions.Value;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<BriefingAiResult> GenerateBriefingAsync(
        BriefingAiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await AiServiceCall.PostAsync(
            _httpClient,
            _tenantContext,
            AiServiceCall.BuildRequestUri(_configuration.BaseUrl, _configuration.BriefingPath),
            request,
            cancellationToken);

        return await AiServiceCall.ReadResultAsync<BriefingAiResult>(
            response, _logger, FailureLogTemplate, ServiceLabel, cancellationToken);
    }
}
