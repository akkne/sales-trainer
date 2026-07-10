namespace Sellevate.Ai.Features.Companies.Models;

public sealed record GeneratePersonaRequestDto(
    string CompanyDescription,
    string? ContactName,
    string? ContactPosition,
    PersonaDifficulty Difficulty);
