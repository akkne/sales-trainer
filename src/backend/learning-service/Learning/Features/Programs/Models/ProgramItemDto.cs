namespace Sellevate.Learning.Features.Programs.Models;

/// <summary>
/// Phase 40.17. One entry of a programme, as the API returns it. <see cref="LessonVersionNumber"/>
/// and <see cref="LessonTitle"/> are read from the pinned snapshot rather than from the live lesson
/// row, and are null when the snapshot is no longer visible to the caller — a state worth showing
/// as "unknown" rather than papering over with the current title, which is exactly the substitution
/// this whole phase exists to stop.
/// </summary>
public record ProgramItemDto(
    Guid Id,
    Guid SkillId,
    Guid LessonId,
    Guid LessonVersionId,
    int? LessonVersionNumber,
    string? LessonTitle,
    int OrderIndex
);
