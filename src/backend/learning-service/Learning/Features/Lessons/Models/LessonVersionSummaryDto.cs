namespace Sellevate.Learning.Features.Lessons.Models;

/// <summary>
/// Phase 40.15. A version's metadata without its body. The list endpoint returns these because a
/// lesson's history is read far more often than any single snapshot inside it, and the snapshots
/// are the only large thing in the table.
/// </summary>
public record LessonVersionSummaryDto(
    Guid Id,
    Guid LessonId,
    int VersionNumber,
    string Status,
    string ContentHash,
    Guid? BaseVersionId,
    bool IsBreaking,
    Guid? CreatedBy,
    DateTime CreatedAt,
    DateTime? PublishedAt
);
