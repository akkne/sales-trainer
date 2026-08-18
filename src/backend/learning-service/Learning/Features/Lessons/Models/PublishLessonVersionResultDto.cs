namespace Sellevate.Learning.Features.Lessons.Models;

/// <summary>
/// Phase 40.15. <see cref="CreatedNewVersion"/> is <see langword="false"/> when the content hash
/// matched the last published version — nothing actually changed, so nothing was frozen. The caller
/// gets the existing published version back and can say "no changes to publish" rather than showing
/// a version number that did not move and leaving the admin to wonder.
/// </summary>
public record PublishLessonVersionResultDto(
    LessonVersionDto Version,
    bool CreatedNewVersion
);
