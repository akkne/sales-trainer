using Microsoft.Extensions.Options;
using Sellevate.Learning.Infrastructure.Configuration;

namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// Registers every typed client that talks to ai-service. They share one configuration section and one
/// service-to-service handshake, and differ only in how long they are allowed to take.
///
/// <para>
/// <b>The handshake.</b> Mirrors ai-service's <c>InternalServiceAuthFilter</c> (which guards
/// <c>EvaluationController</c>) and company-service's Ai clients: the call is rejected without the
/// internal-secret header once <c>InternalAuth:ServiceSecret</c> is configured, and the route is left
/// open in development / single-service mode when the secret is unset. The header is therefore attached
/// only when a secret exists, rather than sending an empty credential.
/// </para>
///
/// <para>
/// <b>The timeouts.</b> Phase 40.27's content pipeline gets one of its own because generating a lesson
/// takes minutes and <see cref="HttpClient"/>'s 100-second default would abandon a call the provider has
/// already been paid for. Phase 40.33's quota preflight gets the opposite: it is an optimisation a sweep
/// asks before it claims work, so a slow answer must not delay the tick it was meant to save. Both are
/// clamped, so a misconfigured value cannot produce a client that times out instantly or never.
/// </para>
/// </summary>
public static class AiEvaluationServiceCollectionExtensions
{
    private const string InternalServiceSecretHeaderName = "X-Internal-Service-Secret";
    private const string InternalServiceSecretConfigurationKey = "InternalAuth:ServiceSecret";

    private const int MinimumContentPipelineTimeoutSeconds = 30;
    private const int MaximumContentPipelineTimeoutSeconds = 900;
    private const int QuotaPreflightTimeoutSeconds = 10;

    public static IServiceCollection AddAiEvaluationClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AiServiceConfiguration>(
            configuration.GetSection(AiServiceConfiguration.SectionName));

        var internalServiceSecret = configuration[InternalServiceSecretConfigurationKey];

        services.AddHttpClient<IAiEvaluationClient, AiEvaluationClient>(httpClient =>
            ApplyInternalServiceSecret(httpClient, internalServiceSecret));

        services.AddHttpClient<IAiQuotaClient, AiQuotaClient>(httpClient =>
        {
            httpClient.Timeout = TimeSpan.FromSeconds(QuotaPreflightTimeoutSeconds);
            ApplyInternalServiceSecret(httpClient, internalServiceSecret);
        });

        services.AddHttpClient<IAiContentPipelineClient, AiContentPipelineClient>(
            (serviceProvider, httpClient) =>
            {
                var aiConfiguration = serviceProvider
                    .GetRequiredService<IOptions<AiServiceConfiguration>>().Value;

                httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(
                    aiConfiguration.ContentPipelineTimeoutSeconds,
                    MinimumContentPipelineTimeoutSeconds,
                    MaximumContentPipelineTimeoutSeconds));

                ApplyInternalServiceSecret(httpClient, internalServiceSecret);
            });

        return services;
    }

    private static void ApplyInternalServiceSecret(HttpClient httpClient, string? internalServiceSecret)
    {
        if (!string.IsNullOrWhiteSpace(internalServiceSecret))
        {
            httpClient.DefaultRequestHeaders.Add(InternalServiceSecretHeaderName, internalServiceSecret);
        }
    }
}
