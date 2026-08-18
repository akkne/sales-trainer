using System.ComponentModel.DataAnnotations;
using Sellevate.Company.Common.Constants;

namespace Sellevate.Company.Features.Companies.Models;

public sealed record CreateCompanyRequestDto(
    [Required][MaxLength(CompanyFieldLengths.Name)] string Name,
    [MaxLength(CompanyFieldLengths.CompanyDescription)] string? Description = null);
