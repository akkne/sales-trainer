namespace Sellevate.Learning.Features.Programs.Models;

/// <summary>
/// Phase 40.17. What a learner sees about their own programme: the snapshot they are pinned to, and
/// — only if a newer published version exists — what switching to it would change.
///
/// <para>
/// <b><see cref="IsEnrolled"/> false is a normal answer, not an error.</b> An organization that has
/// never published a programme version has no pins, and its people keep reading the live library
/// exactly as they did before this phase. Making enrollment a precondition for reaching lessons
/// would have turned "the РОП has not built a programme yet" into "nobody can train", which is a
/// fail-closed choice worth making for data and not for a curriculum (docs/DECISIONS.md,
/// 2026-08-17).
/// </para>
/// </summary>
public record MyProgramDto(
    bool IsEnrolled,
    Guid? ProgramVersionId,
    int? ProgramVersionNumber,
    DateTime? EnrolledAt,
    DateTime? SwitchedAt,
    IReadOnlyList<ProgramItemDto> Items,
    Guid? LatestPublishedProgramVersionId,
    int? LatestPublishedProgramVersionNumber,
    bool SwitchAvailable,
    ProgramDiffDto? PendingDiff
);
