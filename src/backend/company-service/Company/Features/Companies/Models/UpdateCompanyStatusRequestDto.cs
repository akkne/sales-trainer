using System.ComponentModel.DataAnnotations;

namespace Sellevate.Company.Features.Companies.Models;

public sealed record UpdateCompanyStatusRequestDto(
    [property: Required] CompanyStatus? Status);
