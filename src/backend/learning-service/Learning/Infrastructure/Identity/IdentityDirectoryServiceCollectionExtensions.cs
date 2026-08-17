using Sellevate.Learning.Infrastructure.Configuration;

namespace Sellevate.Learning.Infrastructure.Identity;

public static class IdentityDirectoryServiceCollectionExtensions
{
    private const string InternalServiceSecretHeaderName = "X-Internal-Service-Secret";

    public static IServiceCollection AddIdentityDirectoryClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<IdentityServiceConfiguration>(
            configuration.GetSection(IdentityServiceConfiguration.SectionName));

        services.AddHttpClient<IOrganizationMemberDirectory, IdentityOrganizationMemberDirectory>(httpClient =>
        {
            // Same handshake as the ai-service client next door: identity-service's
            // InternalServiceAuthFilter rejects the call without this header once
            // InternalAuth:ServiceSecret is configured, and leaves the route open when it is not
            // (dev / single-service mode).
            var internalServiceSecret = configuration["InternalAuth:ServiceSecret"];
            if (!string.IsNullOrWhiteSpace(internalServiceSecret))
            {
                httpClient.DefaultRequestHeaders.Add(InternalServiceSecretHeaderName, internalServiceSecret);
            }

            var timeoutSeconds = configuration.GetValue<int?>(
                $"{IdentityServiceConfiguration.SectionName}:TimeoutSeconds") ?? 10;
            httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 60));
        });

        return services;
    }
}
