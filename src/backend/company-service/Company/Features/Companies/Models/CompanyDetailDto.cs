namespace Sellevate.Company.Features.Companies.Models;

public sealed record CompanyDetailDto(
    Guid Id,
    string Name,
    string Description,
    CompanyStatus Status,
    int CallLogCount,
    int PracticeCallCount,
    int ContactCount,
    DateTime? NextActionAt,
    string? NextActionNote,
    DateTime? FollowUpNotifiedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
