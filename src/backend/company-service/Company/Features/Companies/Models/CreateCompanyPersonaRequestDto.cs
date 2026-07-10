using System.ComponentModel.DataAnnotations;

namespace Sellevate.Company.Features.Companies.Models;

public sealed record CreateCompanyPersonaRequestDto(
    [Required][MaxLength(200)] string Name,
    [Required][MaxLength(200)] string Position,
    [Required][MaxLength(4000)] string Personality,
    PersonaDifficulty Difficulty);
