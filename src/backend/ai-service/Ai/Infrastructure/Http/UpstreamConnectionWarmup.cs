using Microsoft.Extensions.Options;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Infrastructure.Http;

/// <summary>
/// Keeps TCP+TLS connections to upstream AI services (OpenAI, Yandex TTS, Google TTS)
/// warm, so the first dialog turn after an idle period does not pay the handshake
/// cost (~100–300ms). A HEAD request is enough — any response means the connection
/// is established and pooled.
/// </summary>
internal sealed class UpstreamConnectionWarmup
{
    private static readonly TimeSpan PerTargetTimeout = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<OpenAiConfiguration> _openAiOptions;
    private readonly IOptions<YandexTtsConfiguration> _yandexTtsOptions;
    private readonly IOptions<GoogleTtsConfiguration> _googleTtsOptions;
    private readonly ILogger<UpstreamConnectionWarmup> _logger;

    public UpstreamConnectionWarmup(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenAiConfiguration> openAiOptions,
        IOptions<YandexTtsConfiguration> yandexTtsOptions,
        IOptions<GoogleTtsConfiguration> googleTtsOptions,
        ILogger<UpstreamConnectionWarmup> logger)
    {
        _httpClientFactory = httpClientFactory;
        _openAiOptions = openAiOptions;
        _yandexTtsOptions = yandexTtsOptions;
        _googleTtsOptions = googleTtsOptions;
        _logger = logger;
    }

    public IReadOnlyList<(string ClientName, string BaseUrl)> ResolveTargets()
    {
        var targets = new List<(string, string)>();

        if (IsApiKeyConfigured(_openAiOptions.Value.ApiKey))
            targets.Add(("OpenAI", _openAiOptions.Value.BaseUrl));
        if (IsApiKeyConfigured(_yandexTtsOptions.Value.ApiKey))
            targets.Add(("YandexTts", _yandexTtsOptions.Value.BaseUrl));
        if (IsApiKeyConfigured(_googleTtsOptions.Value.ApiKey))
            targets.Add(("GoogleTts", "https://texttospeech.googleapis.com"));

        return targets;
    }

    /// <summary>Pings every configured upstream once; failures are logged and swallowed.</summary>
    public async Task<int> WarmupOnceAsync(CancellationToken cancellationToken)
    {
        var targets = ResolveTargets();
        foreach (var (clientName, baseUrl) in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(PerTargetTimeout);

                using var request = new HttpRequestMessage(HttpMethod.Head, baseUrl);
                using var response = await _httpClientFactory.CreateClient(clientName)
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Connection warmup for {ClientName} ({BaseUrl}) failed", clientName, baseUrl);
            }
        }

        return targets.Count;
    }

    private static bool IsApiKeyConfigured(string? apiKey) =>
        !string.IsNullOrWhiteSpace(apiKey) &&
        !apiKey.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase);
}
