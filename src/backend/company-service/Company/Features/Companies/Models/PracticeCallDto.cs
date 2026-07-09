namespace Sellevate.Company.Features.Companies.Models;

public sealed record PracticeCallDto(
    Guid Id,
    Guid CompanyId,
    string DialogSessionId,
    string Goal,
    DateTime CreatedAt);
