namespace Sellevate.Company.Features.Companies.Models;

public sealed record CompanyContactDto(
    Guid Id,
    Guid CompanyId,
    string Name,
    string Position,
    string Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);
