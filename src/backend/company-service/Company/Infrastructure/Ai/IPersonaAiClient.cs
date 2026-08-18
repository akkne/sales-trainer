namespace Sellevate.Company.Infrastructure.Ai;

/// <summary>
/// Drafts a buyer persona for a practice call via ai-service. Persists nothing: the returned draft
/// only becomes a stored persona if the caller saves it.
/// </summary>
public interface IPersonaAiClient
{
    Task<PersonaAiResult> GeneratePersonaAsync(
        PersonaAiRequest request,
        CancellationToken cancellationToken = default);
}
