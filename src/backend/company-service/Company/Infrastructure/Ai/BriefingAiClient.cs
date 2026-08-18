using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Company.Infrastructure.Configuration;

namespace Sellevate.Company.Infrastructure.Ai;

internal sealed class BriefingAiClient : IBriefingAiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

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

        var requestUri = _configuration.BaseUrl.TrimEnd('/') + _configuration.BriefingPath;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(request, options: SerializerOptions),
        };

        // Phase 40.33. ai-service meters LLM spend per organization and refuses a call that names
        // none, so the tenant travels with the request rather than being inferred there.
        AiCallHeaders.Apply(httpRequest, _tenantContext);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "AI briefing generation returned {StatusCode}: {Body}",
                response.StatusCode, responseBody);
            throw new InvalidOperationException(
                $"AI briefing service returned {(int)response.StatusCode}.");
        }

        var result = await response.Content.ReadFromJsonAsync<BriefingAiResult>(
            SerializerOptions, cancellationToken);

        if (result is null)
            throw new InvalidOperationException("AI briefing service returned an empty body.");

        return result;
    }
}
