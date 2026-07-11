using System.ComponentModel.DataAnnotations;

namespace Sellevate.Company.Features.Companies.Models;

public sealed record CreateCompanyContactRequestDto(
    [Required][MaxLength(200)] string Name,
    [MaxLength(200)] string? Position = null,
    [MaxLength(2000)] string? Notes = null);
