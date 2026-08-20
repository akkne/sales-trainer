using Microsoft.Extensions.Options;
using Sellevate.Organization.Infrastructure.Configuration;

namespace Sellevate.Organization.Infrastructure.Identity;

/// <summary>
/// Registers the typed <see cref="HttpClient"/> behind <see cref="IIdentityOrganizationBootstrapClient"/>.
/// Copied verbatim from learning-service's <c>IdentityDirectoryServiceCollectionExtensions</c>: the
/// header is attached only when a secret is actually configured, so an environment that has not
/// provisioned <c>INTERNAL_SERVICE_SECRET</c> yet (docs/DONT_FORGET.md) keeps working in development
/// rather than sending an empty credential, and the timeout is clamped so a misconfigured value cannot
/// produce a client that fails instantly or never.
/// </summary>
public static class IdentityOrganizationBootstrapClientServiceCollectionExtensions
{
    private const string InternalServiceSecretHeaderName = "X-Internal-Service-Secret";
    private const string InternalServiceSecretConfigurationKey = "InternalAuth:ServiceSecret";
    private const int MinimumTimeoutSeconds = 1;
    private const int MaximumTimeoutSeconds = 60;

    public static IServiceCollection AddIdentityOrganizationBootstrapClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<IdentityServiceConfiguration>(
            configuration.GetSection(IdentityServiceConfiguration.SectionName));

        services.AddHttpClient<IIdentityOrganizationBootstrapClient, IdentityOrganizationBootstrapClient>(
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
