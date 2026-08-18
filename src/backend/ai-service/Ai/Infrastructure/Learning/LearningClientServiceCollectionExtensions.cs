namespace Sellevate.Ai.Infrastructure.Learning;

public static class LearningClientServiceCollectionExtensions
{
    private const string InternalServiceSecretHeaderName = "X-Internal-Service-Secret";

    public static IServiceCollection AddLearningAssignmentClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<LearningServiceConfiguration>(
            configuration.GetSection(LearningServiceConfiguration.SectionName));

        services.AddHttpClient<IAssignmentPracticeContextClient, AssignmentPracticeContextClient>(httpClient =>
        {
            // The same handshake ai-service demands of its own callers, pointed the other way:
            // learning-service's InternalServiceAuthFilter rejects the call without this header once
            // InternalAuth:ServiceSecret is configured, and leaves the route open when it is not.
            var internalServiceSecret = configuration["InternalAuth:ServiceSecret"];
            if (!string.IsNullOrWhiteSpace(internalServiceSecret))
            {
                httpClient.DefaultRequestHeaders.Add(InternalServiceSecretHeaderName, internalServiceSecret);
            }

            var timeoutSeconds = configuration.GetValue<int?>(
                $"{LearningServiceConfiguration.SectionName}:TimeoutSeconds") ?? 5;
            httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 30));
        });

        return services;
    }
}
