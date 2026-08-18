namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// Phase 40.21. The <c>&lt;kind&gt;:&lt;id&gt;</c> namespace an assignment uses when its source is a
/// frozen lesson snapshot — the sibling of <see cref="SkillGapSourceRefs"/> in the same
/// <c>Assignment.SourceRef</c> vocabulary (docs/TENANCY/ASSIGNMENTS.md §1).
///
/// <para>
/// <b>The version, never the lesson.</b> A <c>lesson-version:&lt;uuid&gt;</c> reference still means in a
/// year what it meant on the day it was written, while a lesson id silently re-points at whatever the
/// lesson has since become. That is the same rule 40.15 and 40.16 exist to enforce, expressed in the one
/// column that records where an assignment came from.
/// </para>
///
/// <para>
/// The value is persisted, so it must never change: rows written by earlier versions of this service
/// carry it verbatim.
/// </para>
/// </summary>
public static class LessonVersionSourceRefs
{
    /// <summary>The namespace. Everything after it is the <c>LessonVersions.Id</c>.</summary>
    public const string Kind = "lesson-version";

    /// <summary><c>lesson-version:&lt;uuid&gt;</c>, inside the 200 characters <c>Assignments.SourceRef</c> allows.</summary>
    public static string Build(Guid lessonVersionId) => $"{Kind}:{lessonVersionId}";
}
