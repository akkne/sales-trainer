using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Company.Infrastructure.Configuration;

namespace Sellevate.Company.Infrastructure.Ai;

/// <summary>
/// Calls ai-service to turn pasted raw notes or a transcript into the fields of a draft call-log
/// entry. Persists nothing and asserts nothing about accuracy: the result is a draft a person
/// reviews before it is saved, which is why a partially filled answer is acceptable here while an
/// unnoticed failure is not.
/// </summary>
internal sealed class ParseLogAiClient : IParseLogAiClient
{
    private const string FailureLogTemplate = "AI call log parsing returned {StatusCode}: {Body}";
    private const string ServiceLabel = "AI parse-log service";

    private readonly HttpClient _httpClient;
    private readonly AiServiceConfiguration _configuration;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ParseLogAiClient> _logger;

    public ParseLogAiClient(
        HttpClient httpClient,
        IOptions<AiServiceConfiguration> configurationOptions,
        ITenantContext tenantContext,
        ILogger<ParseLogAiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configurationOptions.Value;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<ParseLogAiResult> ParseLogAsync(
        ParseLogAiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await AiServiceCall.PostAsync(
            _httpClient,
            _tenantContext,
            AiServiceCall.BuildRequestUri(_configuration.BaseUrl, _configuration.ParseLogPath),
            request,
            cancellationToken);

        return await AiServiceCall.ReadResultAsync<ParseLogAiResult>(
            response, _logger, FailureLogTemplate, ServiceLabel, cancellationToken);
    }
}
