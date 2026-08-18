using Microsoft.Extensions.Options;
using Sellevate.Ai.Common.Constants;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Infrastructure.Http;

/// <summary>
/// Keeps TCP+TLS connections to upstream AI services (OpenAI, Yandex TTS)
/// warm, so the first dialog turn after an idle period does not pay the handshake
/// cost (~100–300ms). A HEAD request is enough — any response means the connection
/// is established and pooled.
///
/// <para>
/// A provider with no real key is not warmed: there is nothing to warm it for, and sending an
/// unauthenticated probe to a paid endpoint on a timer is noise in somebody else's logs.
/// </para>
/// </summary>
internal sealed class UpstreamConnectionWarmup
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<OpenAiConfiguration> _openAiOptions;
    private readonly IOptions<YandexTtsConfiguration> _yandexTtsOptions;
    private readonly IOptions<UpstreamWarmupConfiguration> _warmupOptions;
    private readonly ILogger<UpstreamConnectionWarmup> _logger;

    public UpstreamConnectionWarmup(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenAiConfiguration> openAiOptions,
        IOptions<YandexTtsConfiguration> yandexTtsOptions,
        IOptions<UpstreamWarmupConfiguration> warmupOptions,
        ILogger<UpstreamConnectionWarmup> logger)
    {
        _httpClientFactory = httpClientFactory;
        _openAiOptions = openAiOptions;
        _yandexTtsOptions = yandexTtsOptions;
        _warmupOptions = warmupOptions;
        _logger = logger;
    }

    public IReadOnlyList<(string ClientName, string BaseUrl)> ResolveTargets()
    {
        var targets = new List<(string, string)>();

        if (IsApiKeyConfigured(_openAiOptions.Value.ApiKey))
            targets.Add((AiProviderHttpConstants.OpenAiClientName, _openAiOptions.Value.BaseUrl));
        if (IsApiKeyConfigured(_yandexTtsOptions.Value.ApiKey))
            targets.Add((AiProviderHttpConstants.YandexTtsClientName, _yandexTtsOptions.Value.BaseUrl));

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
                timeout.CancelAfter(TimeSpan.FromSeconds(_warmupOptions.Value.PerTargetTimeoutSeconds));

                using var request = new HttpRequestMessage(HttpMethod.Head, baseUrl);
                using var response = await _httpClientFactory.CreateClient(clientName)
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogDebug(
                    exception, "Connection warmup for {ClientName} ({BaseUrl}) failed", clientName, baseUrl);
            }
        }

        return targets.Count;
    }

    private static bool IsApiKeyConfigured(string? apiKey) =>
        AiSecretPlaceholders.IsRealSecret(apiKey);
}
