namespace Sellevate.Company.Infrastructure.Ai;

/// <summary>Registers <see cref="IPersonaAiClient"/> as a typed client against ai-service.</summary>
public static class PersonaAiServiceCollectionExtensions
{
    public static IServiceCollection AddPersonaAiClient(
        this IServiceCollection services,
        IConfiguration configuration)
        => services.AddInternalAiClient<IPersonaAiClient, PersonaAiClient>(configuration);
}
