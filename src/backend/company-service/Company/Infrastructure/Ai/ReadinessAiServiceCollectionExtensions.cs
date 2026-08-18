namespace Sellevate.Company.Infrastructure.Ai;

/// <summary>Registers <see cref="IReadinessAiClient"/> as a typed client against ai-service.</summary>
public static class ReadinessAiServiceCollectionExtensions
{
    public static IServiceCollection AddReadinessAiClient(
        this IServiceCollection services,
        IConfiguration configuration)
        => services.AddInternalAiClient<IReadinessAiClient, ReadinessAiClient>(configuration);
}
