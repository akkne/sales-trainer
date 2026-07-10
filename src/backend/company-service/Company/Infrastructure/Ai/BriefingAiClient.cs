using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.Company.Infrastructure.Configuration;

namespace Sellevate.Company.Infrastructure.Ai;

internal sealed class BriefingAiClient : IBriefingAiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly AiServiceConfiguration _configuration;
    private readonly ILogger<BriefingAiClient> _logger;

    public BriefingAiClient(
        HttpClient httpClient,
        IOptions<AiServiceConfiguration> configurationOptions,
        ILogger<BriefingAiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configurationOptions.Value;
        _logger = logger;
    }

    public async Task<BriefingAiResult> GenerateBriefingAsync(
        BriefingAiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestUri = _configuration.BaseUrl.TrimEnd('/') + _configuration.BriefingPath;

        using var response = await _httpClient.PostAsJsonAsync(
            requestUri, request, SerializerOptions, cancellationToken);

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
