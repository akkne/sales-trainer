using System.ComponentModel.DataAnnotations;
using Sellevate.Company.Common.Constants;

namespace Sellevate.Company.Features.Companies.Models;

public sealed record CreateCompanyPersonaRequestDto(
    [Required][MaxLength(CompanyFieldLengths.Name)] string Name,
    [Required][MaxLength(CompanyFieldLengths.Position)] string Position,
    [Required][MaxLength(CompanyFieldLengths.PersonaPersonality)] string Personality,
    PersonaDifficulty Difficulty);
