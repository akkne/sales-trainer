using Sellevate.Company.Infrastructure.Configuration;

namespace Sellevate.Company.Infrastructure.Ai;

public static class PersonaAiServiceCollectionExtensions
{
    private const string InternalServiceSecretHeaderName = "X-Internal-Service-Secret";

    public static IServiceCollection AddPersonaAiClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AiServiceConfiguration>(
            configuration.GetSection(AiServiceConfiguration.SectionName));

        services.AddHttpClient<IPersonaAiClient, PersonaAiClient>(httpClient =>
        {
            // Service-to-service auth: mirrors ai-service's InternalServiceAuthFilter, which
            // rejects requests without this header once InternalAuth:ServiceSecret is configured
            // (left open in dev/single-service mode when the secret is unset).
            var internalServiceSecret = configuration["InternalAuth:ServiceSecret"];
            if (!string.IsNullOrWhiteSpace(internalServiceSecret))
                httpClient.DefaultRequestHeaders.Add(InternalServiceSecretHeaderName, internalServiceSecret);
        });

        return services;
    }
}
