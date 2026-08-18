using Microsoft.Extensions.Http.Resilience;
using Sellevate.Ai.Common.Constants;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Infrastructure.Http;

/// <summary>
/// Registers the two named clients every paid provider call goes through, the resilience pipeline in
/// front of them, and the background warmup that keeps their sockets open.
///
/// <para>
/// Both clients are configured identically and by name (AI6). A service that resolves a client under a
/// name that is not registered here does not fail loudly — <see cref="IHttpClientFactory"/> hands back a
/// default <see cref="HttpClient"/> with no timeout, no retry policy and no breaker — so the mistake
/// surfaces as an unbounded hang against a paid endpoint. <c>AiProviderHttpConstants</c> exists to keep
/// the two ends of that lookup spelled the same.
/// </para>
/// </summary>
public static class UpstreamHttpClientServiceCollectionExtensions
{
    public static IServiceCollection AddUpstreamHttpClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<UpstreamResilienceConfiguration>(
            configuration.GetSection(UpstreamResilienceConfiguration.SectionName));
        services.Configure<UpstreamWarmupConfiguration>(
            configuration.GetSection(UpstreamWarmupConfiguration.SectionName));

        var resilience = configuration
            .GetSection(UpstreamResilienceConfiguration.SectionName)
            .Get<UpstreamResilienceConfiguration>() ?? new UpstreamResilienceConfiguration();

        string[] upstreamClientNames =
        [
            AiProviderHttpConstants.OpenAiClientName,
            AiProviderHttpConstants.YandexTtsClientName,
        ];

        foreach (var upstreamClientName in upstreamClientNames)
        {
            services.AddHttpClient(upstreamClientName)
                .ConfigureHttpClient(client =>
                    client.Timeout = TimeSpan.FromSeconds(resilience.HandlerTimeoutSeconds))
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    PooledConnectionIdleTimeout =
                        TimeSpan.FromMinutes(resilience.PooledConnectionIdleTimeoutMinutes),
                    PooledConnectionLifetime =
                        TimeSpan.FromMinutes(resilience.PooledConnectionLifetimeMinutes),
                })
                .SetHandlerLifetime(TimeSpan.FromMinutes(resilience.HandlerLifetimeMinutes))
                .AddStandardResilienceHandler(options =>
                {
                    options.AttemptTimeout.Timeout =
                        TimeSpan.FromSeconds(resilience.AttemptTimeoutSeconds);
                    options.Retry.MaxRetryAttempts = resilience.MaximumRetryAttempts;
                    options.Retry.Delay = TimeSpan.FromSeconds(resilience.RetryDelaySeconds);
                    options.CircuitBreaker.SamplingDuration =
                        TimeSpan.FromSeconds(resilience.CircuitBreakerSamplingSeconds);
                    options.CircuitBreaker.MinimumThroughput = resilience.CircuitBreakerMinimumThroughput;
                    options.TotalRequestTimeout.Timeout =
                        TimeSpan.FromSeconds(resilience.TotalRequestTimeoutSeconds);
                });
        }

        services.AddHttpClient();
        services.AddSingleton<UpstreamConnectionWarmup>();
        services.AddHostedService<UpstreamConnectionWarmupService>();

        return services;
    }
}
