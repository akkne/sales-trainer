using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Company.Infrastructure.Configuration;

namespace Sellevate.Company.Infrastructure.Ai;

/// <summary>
/// Calls ai-service to draft a buyer persona — name, position, personality — for a practice call
/// against a company, optionally seeded from a real contact. Persists nothing; the caller decides
/// whether the draft becomes a saved persona.
/// </summary>
internal sealed class PersonaAiClient : IPersonaAiClient
{
    private const string FailureLogTemplate = "AI persona generation returned {StatusCode}: {Body}";
    private const string ServiceLabel = "AI persona service";

    private readonly HttpClient _httpClient;
    private readonly AiServiceConfiguration _configuration;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<PersonaAiClient> _logger;

    public PersonaAiClient(
        HttpClient httpClient,
        IOptions<AiServiceConfiguration> configurationOptions,
        ITenantContext tenantContext,
        ILogger<PersonaAiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configurationOptions.Value;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<PersonaAiResult> GeneratePersonaAsync(
        PersonaAiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await AiServiceCall.PostAsync(
            _httpClient,
            _tenantContext,
            AiServiceCall.BuildRequestUri(_configuration.BaseUrl, _configuration.PersonaPath),
            request,
            cancellationToken);

        return await AiServiceCall.ReadResultAsync<PersonaAiResult>(
            response, _logger, FailureLogTemplate, ServiceLabel, cancellationToken);
    }
}
