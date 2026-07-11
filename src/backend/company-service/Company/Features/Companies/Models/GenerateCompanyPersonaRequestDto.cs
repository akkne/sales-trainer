using System.ComponentModel.DataAnnotations;

namespace Sellevate.Company.Features.Companies.Models;

public sealed record GenerateCompanyPersonaRequestDto(
    [MaxLength(200)] string? ContactName,
    [MaxLength(200)] string? ContactPosition,
    PersonaDifficulty Difficulty);
