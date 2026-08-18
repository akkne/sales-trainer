using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Learning.Features.Programs.Models;

/// <summary>
/// Phase 40.17. One lesson's place in one programme version: which skill it belongs to, where it
/// sits in the running order, and — the point of the whole block — <b>which frozen version of the
/// lesson</b> the learner pinned to this programme will be shown
/// (docs/TENANCY/CONTENT_MODEL.md §2.5).
///
/// <para>
/// <b>Nothing here is content, and that is the design.</b> Every column is a reference. Reordering
/// skills produces a new programme version whose items carry different <see cref="OrderIndex"/>
/// values; no lesson row, no exercise row and no <c>LessonVersion</c> row is written. That is what
/// keeps a curriculum edit from becoming a content fork.
/// </para>
/// </summary>
public sealed class ProgramItem : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>
    /// Denormalized copy of the owning <see cref="ProgramVersion"/>'s organization. Denormalized on
    /// purpose — a row-level-security policy can only compare columns of the row it is filtering, so
    /// the isolation boundary needs the value here and not one join away (docs/TENANCY/TENANCY.md
    /// §1.5). A programme version never changes owner, so the copy cannot drift.
    /// </summary>
    public Guid OrganizationId { get; set; }

    public Guid ProgramVersionId { get; set; }

    /// <summary>
    /// The skill this lesson is taught under, used for grouping and as the unit a "reorder the
    /// skills" edit moves. No foreign key: <c>Skills</c> is a content table under an
    /// <c>IS NULL OR = current</c> policy while this is strict tenant data under plain equality, and
    /// a constraint spanning the two would be validated with the writer's privileges — the same call
    /// 40.16 made for <c>UserExerciseAttempt.LessonVersionId</c> (docs/DECISIONS.md, 2026-08-17).
    /// </summary>
    public Guid SkillId { get; set; }

    /// <summary>
    /// The lesson's lifeline id, denormalized from the pinned version. It is what makes "the same
    /// lesson, now at a different version" expressible — both in the unique constraint that stops a
    /// programme listing one lesson twice at two versions, and in the diff a learner is shown before
    /// switching. <c>LessonVersion.LessonId</c> is frozen by 40.15's trigger, so the copy cannot
    /// drift.
    /// </summary>
    public Guid LessonId { get; set; }

    /// <summary>
    /// The frozen snapshot this programme serves — not the lesson, the version. A learner pinned to
    /// this programme sees this content until they explicitly switch, however many times the lesson
    /// is republished in the meantime.
    ///
    /// <para>
    /// Points at <c>LessonVersions.Id</c> and survives 40.18 re-pointing that row's
    /// <c>BaseVersionId</c>: a published lesson version is immutable, and the column 40.18 rewrites
    /// is provenance, not identity.
    /// </para>
    /// </summary>
    public Guid LessonVersionId { get; set; }

    /// <summary>Position in the programme's running order, zero-based and dense within a version.</summary>
    public int OrderIndex { get; set; }

    public ProgramVersion? ProgramVersion { get; set; }
}
