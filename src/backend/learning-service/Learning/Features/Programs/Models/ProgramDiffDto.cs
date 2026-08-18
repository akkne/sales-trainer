namespace Sellevate.Learning.Features.Programs.Models;

/// <summary>
/// Phase 40.17. What changes if a learner moves from one programme version to another
/// (docs/TENANCY/CONTENT_MODEL.md §2.5: "existing learners are offered an explicit switch with a
/// diff").
///
/// <para>
/// Four buckets rather than one list, because they mean four different things to the person
/// deciding. Lessons appear and disappear; a lesson can stay and be pinned to a newer snapshot; and
/// a lesson can stay at the same snapshot and simply move, which is the commonest edit of all — a
/// РОП reordering skills — and the one that must be visible as "nothing you have learned changed,
/// the order did".
/// </para>
/// </summary>
public record ProgramDiffDto(
    Guid FromProgramVersionId,
    int FromVersionNumber,
    Guid ToProgramVersionId,
    int ToVersionNumber,
    IReadOnlyList<ProgramDiffLessonDto> AddedLessons,
    IReadOnlyList<ProgramDiffLessonDto> RemovedLessons,
    IReadOnlyList<ProgramDiffVersionChangeDto> ChangedLessons,
    IReadOnlyList<ProgramDiffMoveDto> MovedLessons,
    bool HasBreakingChanges
);

/// <summary>A lesson that exists on only one side of the diff.</summary>
public record ProgramDiffLessonDto(
    Guid LessonId,
    Guid SkillId,
    Guid LessonVersionId,
    int? LessonVersionNumber,
    string? LessonTitle,
    int OrderIndex
);

/// <summary>
/// A lesson kept by both programmes but pinned to a different snapshot.
///
/// <para>
/// <see cref="IsBreaking"/> is not read off the target version alone. A programme can skip several
/// lesson versions at once, so the honest answer is "did any published version of this lesson
/// between the two pins declare itself breaking" — every version strictly after the lower of the
/// two version numbers and up to and including the higher. Reading only the target would hide a
/// changed correct answer behind a later typo fix, which is the 40.16 failure restated one level up
/// (docs/TENANCY/CONTENT_MODEL.md §2.4).
/// </para>
/// </summary>
public record ProgramDiffVersionChangeDto(
    Guid LessonId,
    Guid SkillId,
    string? LessonTitle,
    Guid FromLessonVersionId,
    int? FromLessonVersionNumber,
    Guid ToLessonVersionId,
    int? ToLessonVersionNumber,
    bool IsBreaking
);

/// <summary>
/// A lesson kept at the same snapshot that changed place — a different position, a different skill,
/// or both. This is the whole content of a "reorder the skills" edit, and the proof that such an
/// edit touched no lesson.
/// </summary>
public record ProgramDiffMoveDto(
    Guid LessonId,
    string? LessonTitle,
    Guid FromSkillId,
    Guid ToSkillId,
    int FromOrderIndex,
    int ToOrderIndex
);
