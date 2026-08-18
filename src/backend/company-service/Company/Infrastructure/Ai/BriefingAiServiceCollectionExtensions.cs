namespace Sellevate.Company.Infrastructure.Ai;

/// <summary>Registers <see cref="IBriefingAiClient"/> as a typed client against ai-service.</summary>
public static class BriefingAiServiceCollectionExtensions
{
    public static IServiceCollection AddBriefingAiClient(
        this IServiceCollection services,
        IConfiguration configuration)
        => services.AddInternalAiClient<IBriefingAiClient, BriefingAiClient>(configuration);
}
