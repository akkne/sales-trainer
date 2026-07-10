namespace Sellevate.Company.Infrastructure.Ai;

public interface IPersonaAiClient
{
    Task<PersonaAiResult> GeneratePersonaAsync(
        PersonaAiRequest request,
        CancellationToken cancellationToken = default);
}
