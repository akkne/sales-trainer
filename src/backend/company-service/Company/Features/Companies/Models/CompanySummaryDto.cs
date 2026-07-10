namespace Sellevate.Company.Features.Companies.Models;

public sealed record CompanySummaryDto(
    Guid Id,
    string Name,
    string DescriptionExcerpt,
    CompanyStatus Status,
    int CallLogCount,
    int PracticeCallCount,
    int ContactCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);
