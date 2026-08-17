namespace Sellevate.Learning.Features.Admin;

/// <summary>
/// <paramref name="Slug"/> is optional (Phase 40.15): omitted, the lesson gets a collision-free
/// machine slug derived from its own id. Supplied, it must be lowercase latin letters, digits and
/// single hyphens — see <c>LessonSlugGenerator</c> for why it is validated rather than rewritten.
/// On update, omitting it leaves the existing slug alone; renaming a slug is safe today because
/// nothing routes by it.
/// </summary>
public record CreateLessonRequestDto(
    string Title,
    int OrderInTopic,
    string? Slug = null
);
