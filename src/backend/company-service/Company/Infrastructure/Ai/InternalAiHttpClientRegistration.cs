using Sellevate.Company.Common.Constants;
using Sellevate.Company.Infrastructure.Configuration;

namespace Sellevate.Company.Infrastructure.Ai;

/// <summary>
/// One registration recipe for all four typed clients that call ai-service. Each of the four had its
/// own copy of the same eight lines — bind <see cref="AiServiceConfiguration"/>, register the typed
/// client, attach the internal service secret if one is configured — and the copies are exactly the
/// kind that go out of step: a fifth client added by pattern-matching on a neighbour inherits
/// whichever copy it was pasted from, and a client that quietly loses the secret header keeps working
/// in dev (where ai-service leaves its internal endpoints open) and starts returning 401 only in an
/// environment that has the secret set.
/// </summary>
internal static class InternalAiHttpClientRegistration
{
    /// <summary>
    /// Header ai-service's internal-auth filter looks for. It rejects an internal call without it
    /// once <see cref="CompanyConfigurationKeys.InternalServiceSecret"/> is configured on both sides.
    /// </summary>
    public const string InternalServiceSecretHeaderName = "X-Internal-Service-Secret";

    /// <summary>
    /// Binds the shared <see cref="AiServiceConfiguration"/> section and registers
    /// <typeparamref name="TImplementation"/> as the typed client behind
    /// <typeparamref name="TClient"/>. Binding the section once per client is intentional and
    /// idempotent: each entry point stays independently callable rather than depending on some
    /// other registration having run first.
    ///
    /// <para>
    /// The secret is read at registration time, so a value that arrives later in the process
    /// lifetime is not picked up — configuration for this service is supplied at startup.
    /// </para>
    /// </summary>
    public static IServiceCollection AddInternalAiClient<TClient, TImplementation>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TClient : class
        where TImplementation : class, TClient
    {
        services.Configure<AiServiceConfiguration>(
            configuration.GetSection(AiServiceConfiguration.SectionName));

        services.AddHttpClient<TClient, TImplementation>(httpClient =>
        {
            var internalServiceSecret = configuration[CompanyConfigurationKeys.InternalServiceSecret];
            if (!string.IsNullOrWhiteSpace(internalServiceSecret))
                httpClient.DefaultRequestHeaders.Add(InternalServiceSecretHeaderName, internalServiceSecret);
        });

        return services;
    }
}
