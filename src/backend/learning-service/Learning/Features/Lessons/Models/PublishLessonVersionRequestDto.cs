namespace Sellevate.Learning.Features.Lessons.Models;

/// <summary>
/// Phase 40.15. The one thing publishing cannot infer: whether the edit was cosmetic or semantic.
/// A typo fix and a changed correct answer look identical to a diff, and 40.16 joins the accuracy
/// series across the first and splits it across the second, so the publisher has to say which it
/// was. Defaults to <see langword="false"/> because the common publish is a wording fix, and the
/// admin who really did move a correct answer is the one who knows to tick the box.
/// </summary>
public record PublishLessonVersionRequestDto(
    bool IsBreaking = false
);
