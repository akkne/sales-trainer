using Sellevate.Ai.Features.Companies.Models;

namespace Sellevate.Ai.Features.Companies.Services.Abstract;

public interface IPersonaService
{
    /// <summary>
    /// Asks the configured LLM to invent a vivid but realistic buyer persona (name, position,
    /// personality) for a practice call against the given company, optionally seeded from an
    /// existing contact's name/position and tuned by the requested difficulty. Stateless — nothing
    /// is read from or written to a database here. Throws <see cref="InvalidOperationException"/>
    /// if the AI is not configured or its response cannot be parsed as the expected JSON shape.
    /// </summary>
    Task<GeneratedPersonaDto> GeneratePersonaAsync(GeneratePersonaRequestDto request, CancellationToken cancellationToken = default);
}
