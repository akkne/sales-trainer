namespace Sellevate.Company.Features.Companies.Models;

public sealed record CompanyPersonaDto(
    Guid Id,
    Guid CompanyId,
    string Name,
    string Position,
    string Personality,
    PersonaDifficulty Difficulty,
    DateTime CreatedAt);
