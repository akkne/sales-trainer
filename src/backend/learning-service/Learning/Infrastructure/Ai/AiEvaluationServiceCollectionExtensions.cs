using Sellevate.Learning.Infrastructure.Configuration;

namespace Sellevate.Learning.Infrastructure.Ai;

public static class AiEvaluationServiceCollectionExtensions
{
    private const string InternalServiceSecretHeaderName = "X-Internal-Service-Secret";

    public static IServiceCollection AddAiEvaluationClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AiServiceConfiguration>(
            configuration.GetSection(AiServiceConfiguration.SectionName));

        services.AddHttpClient<IAiEvaluationClient, AiEvaluationClient>(httpClient =>
        {
            // Service-to-service auth: mirrors ai-service's InternalServiceAuthFilter (which
            // guards EvaluationController) and company-service's Ai clients, which rejects
            // requests without this header once InternalAuth:ServiceSecret is configured (left
            // open in dev/single-service mode when the secret is unset).
            var internalServiceSecret = configuration["InternalAuth:ServiceSecret"];
            if (!string.IsNullOrWhiteSpace(internalServiceSecret))
                httpClient.DefaultRequestHeaders.Add(InternalServiceSecretHeaderName, internalServiceSecret);
        });

        return services;
    }
}
