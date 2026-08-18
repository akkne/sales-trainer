namespace Sellevate.Learning.Features.Admin;

/// <summary>
/// <paramref name="Slug"/> is optional (Phase 40.15): omitted, the lesson gets a collision-free
/// machine slug derived from its own id. Supplied, it must be lowercase latin letters, digits and
/// single hyphens — see <c>LessonSlugGenerator</c> for why it is validated rather than rewritten.
/// On update, omitting it leaves the existing slug alone; renaming a slug is safe today because
/// nothing routes by it.
/// </summary>
/// <remarks>
/// <paramref name="IsArchived"/> is Phase 40.27 and is honoured on update only. Archiving was
/// reachable from exactly one place before — retiring an override — and a lesson that arrives
/// archived had no way out, which is the state a generated lesson starts in
/// (docs/TENANCY/CONTENT_MODEL.md §5). Omitted, it leaves the flag alone, so the existing callers
/// that send a title and an order keep meaning what they meant.
/// </remarks>
public record CreateLessonRequestDto(
    string Title,
    int OrderInTopic,
    string? Slug = null,
    bool? IsArchived = null
);
