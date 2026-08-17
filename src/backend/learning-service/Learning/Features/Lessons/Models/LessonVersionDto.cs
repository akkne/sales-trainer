using System.Text.Json;

namespace Sellevate.Learning.Features.Lessons.Models;

/// <summary>
/// Phase 40.15. One version including its snapshot. <see cref="Content"/> is a
/// <see cref="JsonElement"/> rather than a string so the body arrives as JSON instead of as a
/// string containing JSON that every caller then has to parse a second time.
/// </summary>
public record LessonVersionDto(
    Guid Id,
    Guid LessonId,
    int VersionNumber,
    string Status,
    JsonElement Content,
    string ContentHash,
    Guid? BaseVersionId,
    bool IsBreaking,
    Guid? CreatedBy,
    DateTime CreatedAt,
    DateTime? PublishedAt
);
