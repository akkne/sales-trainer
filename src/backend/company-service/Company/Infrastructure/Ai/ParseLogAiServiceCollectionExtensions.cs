namespace Sellevate.Company.Infrastructure.Ai;

/// <summary>Registers <see cref="IParseLogAiClient"/> as a typed client against ai-service.</summary>
public static class ParseLogAiServiceCollectionExtensions
{
    public static IServiceCollection AddParseLogAiClient(
        this IServiceCollection services,
        IConfiguration configuration)
        => services.AddInternalAiClient<IParseLogAiClient, ParseLogAiClient>(configuration);
}
