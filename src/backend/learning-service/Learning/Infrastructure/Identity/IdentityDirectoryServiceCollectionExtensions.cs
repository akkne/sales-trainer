using Microsoft.Extensions.Options;
using Sellevate.Learning.Infrastructure.Configuration;

namespace Sellevate.Learning.Infrastructure.Identity;

/// <summary>
/// Registers the typed <see cref="HttpClient"/> that backs <see cref="IOrganizationMemberDirectory"/>.
///
/// <para>
/// The same handshake as the ai-service client next door: identity-service's
/// <c>InternalServiceAuthFilter</c> rejects the call without the internal-secret header once
/// <c>InternalAuth:ServiceSecret</c> is configured, and leaves the route open when it is not
/// (development / single-service mode). The header is therefore attached only when a secret exists,
/// so an unconfigured environment keeps working instead of sending an empty credential.
/// </para>
///
/// <para>
/// The timeout is clamped rather than trusted: a misconfigured zero would make every roster lookup
/// fail instantly and a misconfigured hour would hang the РОП pressing "issue" behind an
/// unresponsive identity-service.
/// </para>
/// </summary>
public static class IdentityDirectoryServiceCollectionExtensions
{
    private const string InternalServiceSecretHeaderName = "X-Internal-Service-Secret";
    private const string InternalServiceSecretConfigurationKey = "InternalAuth:ServiceSecret";
    private const int MinimumTimeoutSeconds = 1;
    private const int MaximumTimeoutSeconds = 60;

    public static IServiceCollection AddIdentityDirectoryClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<IdentityServiceConfiguration>(
            configuration.GetSection(IdentityServiceConfiguration.SectionName));

        services.AddHttpClient<IOrganizationMemberDirectory, IdentityOrganizationMemberDirectory>(
            (serviceProvider, httpClient) =>
            {
                var internalServiceSecret = configuration[InternalServiceSecretConfigurationKey];
                if (!string.IsNullOrWhiteSpace(internalServiceSecret))
                {
                    httpClient.DefaultRequestHeaders.Add(InternalServiceSecretHeaderName, internalServiceSecret);
                }

                var identityConfiguration = serviceProvider
                    .GetRequiredService<IOptions<IdentityServiceConfiguration>>().Value;
                httpClient.Timeout = TimeSpan.FromSeconds(
                    Math.Clamp(identityConfiguration.TimeoutSeconds, MinimumTimeoutSeconds, MaximumTimeoutSeconds));
            });

        return services;
    }
}
