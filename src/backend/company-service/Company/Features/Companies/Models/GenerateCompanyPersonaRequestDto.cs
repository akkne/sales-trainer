using System.ComponentModel.DataAnnotations;
using Sellevate.Company.Common.Constants;

namespace Sellevate.Company.Features.Companies.Models;

public sealed record GenerateCompanyPersonaRequestDto(
    [MaxLength(CompanyFieldLengths.Name)] string? ContactName,
    [MaxLength(CompanyFieldLengths.Position)] string? ContactPosition,
    PersonaDifficulty Difficulty);
