using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellevate.Company.Infrastructure.Configuration;

namespace Sellevate.Company.Infrastructure.Ai;

internal sealed class ParseLogAiClient : IParseLogAiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly AiServiceConfiguration _configuration;
    private readonly ILogger<ParseLogAiClient> _logger;

    public ParseLogAiClient(
        HttpClient httpClient,
        IOptions<AiServiceConfiguration> configurationOptions,
        ILogger<ParseLogAiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configurationOptions.Value;
        _logger = logger;
    }

    public async Task<ParseLogAiResult> ParseLogAsync(
        ParseLogAiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestUri = _configuration.BaseUrl.TrimEnd('/') + _configuration.ParseLogPath;

        using var response = await _httpClient.PostAsJsonAsync(
            requestUri, request, SerializerOptions, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "AI call log parsing returned {StatusCode}: {Body}",
                response.StatusCode, responseBody);
            throw new InvalidOperationException(
                $"AI parse-log service returned {(int)response.StatusCode}.");
        }

        var result = await response.Content.ReadFromJsonAsync<ParseLogAiResult>(
            SerializerOptions, cancellationToken);

        if (result is null)
            throw new InvalidOperationException("AI parse-log service returned an empty body.");

        return result;
    }
}
