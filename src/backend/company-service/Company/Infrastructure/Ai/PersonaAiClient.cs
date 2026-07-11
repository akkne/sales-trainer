using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.Company.Infrastructure.Configuration;

namespace Sellevate.Company.Infrastructure.Ai;

internal sealed class PersonaAiClient : IPersonaAiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly AiServiceConfiguration _configuration;
    private readonly ILogger<PersonaAiClient> _logger;

    public PersonaAiClient(
        HttpClient httpClient,
        IOptions<AiServiceConfiguration> configurationOptions,
        ILogger<PersonaAiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configurationOptions.Value;
        _logger = logger;
    }

    public async Task<PersonaAiResult> GeneratePersonaAsync(
        PersonaAiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestUri = _configuration.BaseUrl.TrimEnd('/') + _configuration.PersonaPath;

        using var response = await _httpClient.PostAsJsonAsync(
            requestUri, request, SerializerOptions, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "AI persona generation returned {StatusCode}: {Body}",
                response.StatusCode, responseBody);
            throw new InvalidOperationException(
                $"AI persona service returned {(int)response.StatusCode}.");
        }

        var result = await response.Content.ReadFromJsonAsync<PersonaAiResult>(
            SerializerOptions, cancellationToken);

        if (result is null)
            throw new InvalidOperationException("AI persona service returned an empty body.");

        return result;
    }
}
