using Sellevate.Ai.Features.Companies.Models;

namespace Sellevate.Ai.Features.Companies.Services.Abstract;

public interface IBriefingService
{
    /// <summary>
    /// Composes a Russian system prompt from the supplied company context and asks the
    /// configured LLM for a short markdown pre-call cheat sheet. Stateless — callers pass in
    /// everything the model needs; nothing is read from a database here.
    /// </summary>
    Task<string> GenerateBriefingAsync(GenerateBriefingRequestDto request, CancellationToken cancellationToken = default);
}
