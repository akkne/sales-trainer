namespace Sellevate.Company.Features.Companies.Models;

public sealed record CallLogEntryDto(
    Guid Id,
    Guid CompanyId,
    string ContactName,
    string Subject,
    string Outcome,
    DateTime OccurredAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? ContactId);
