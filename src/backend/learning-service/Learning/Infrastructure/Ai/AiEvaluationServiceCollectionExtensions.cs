using Sellevate.Learning.Infrastructure.Configuration;

namespace Sellevate.Learning.Infrastructure.Ai;

public static class AiEvaluationServiceCollectionExtensions
{
    public static IServiceCollection AddAiEvaluationClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AiServiceConfiguration>(
            configuration.GetSection(AiServiceConfiguration.SectionName));

        services.AddHttpClient<IAiEvaluationClient, AiEvaluationClient>();

        return services;
    }
}
