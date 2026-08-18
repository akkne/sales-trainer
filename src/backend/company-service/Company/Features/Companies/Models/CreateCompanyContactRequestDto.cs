using System.ComponentModel.DataAnnotations;
using Sellevate.Company.Common.Constants;

namespace Sellevate.Company.Features.Companies.Models;

public sealed record CreateCompanyContactRequestDto(
    [Required][MaxLength(CompanyFieldLengths.Name)] string Name,
    [MaxLength(CompanyFieldLengths.Position)] string? Position = null,
    [MaxLength(CompanyFieldLengths.ContactNotes)] string? Notes = null);
