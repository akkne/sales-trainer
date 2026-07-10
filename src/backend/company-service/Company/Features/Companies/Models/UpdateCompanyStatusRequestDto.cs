using System.ComponentModel.DataAnnotations;

namespace Sellevate.Company.Features.Companies.Models;

public sealed record UpdateCompanyStatusRequestDto(
    [Required] CompanyStatus Status);
