using System.ComponentModel.DataAnnotations;

namespace Sellevate.Company.Features.Companies.Models;

public sealed record CreateCompanyRequestDto(
    [Required][MaxLength(200)] string Name,
    [MaxLength(8000)] string? Description = null);
