namespace Sellevate.Company.Features.Companies.Models;

public sealed record CompanyDetailDto(
    Guid Id,
    string Name,
    string Description,
    int CallLogCount,
    int PracticeCallCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);
