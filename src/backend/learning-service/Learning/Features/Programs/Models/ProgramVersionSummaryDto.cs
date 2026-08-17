namespace Sellevate.Learning.Features.Programs.Models;

/// <summary>
/// Phase 40.17. A programme version's metadata without its items. The list endpoint returns these,
/// for the same reason 40.15's lesson list does: the history is read far more often than any one
/// snapshot in it, and the items are the only thing in the table that grows.
/// </summary>
public record ProgramVersionSummaryDto(
    Guid Id,
    int VersionNumber,
    string Status,
    int ItemCount,
    int EnrollmentCount,
    Guid? CreatedBy,
    DateTime CreatedAt,
    DateTime? PublishedAt
);
