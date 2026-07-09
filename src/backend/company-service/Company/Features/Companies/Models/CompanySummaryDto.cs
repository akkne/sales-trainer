namespace Sellevate.Company.Features.Companies.Models;

public sealed record CompanySummaryDto(
    Guid Id,
    string Name,
    string DescriptionExcerpt,
    int CallLogCount,
    int PracticeCallCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);
