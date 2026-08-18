using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Learning.Features.Lessons.Models;

public sealed class UserExerciseAttempt : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>
    /// Phase 40.10. Owning organization; never null. The security boundary is the Postgres
    /// row-level-security policy created by the AddOrganizationId migration — the EF query
    /// filter on this property is convenience (docs/TENANCY/TENANCY.md 1.4-1.5).
    /// </summary>
    public Guid OrganizationId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// Phase 40.16. The immutable <see cref="LessonVersion"/> this answer was scored against
    /// (docs/TENANCY/CONTENT_MODEL.md §2.3). Together with <see cref="ExerciseId"/> — which is a key
    /// <em>inside</em> that snapshot, since <c>exerciseId</c> is part of the serialized content — it
    /// pins the attempt to the exact question, options and answer key the learner actually saw.
    ///
    /// <para>
    /// Without it an administrator fixing a wrong correct-answer silently re-interprets every
    /// historical attempt, and accuracy-per-skill — the number sold to the РОП as a measure of team
    /// readiness — moves retroactively. The version reference is what makes the number stable: the
    /// edit produces a new version, and the old series keeps pointing at the old snapshot.
    /// </para>
    ///
    /// <para>
    /// Deliberately nullable and deliberately not a foreign key. Nullable, because attempts recorded
    /// before this phase have no version to point at until the backfill script runs
    /// (docs/TENANCY/sql/40.16_progress_version_backfill.sql) — <see langword="null"/> reads as
    /// "unversioned, pre-40.16", which the metrics endpoint reports as its own bucket rather than
    /// folding into a version's series and quietly overstating it. No foreign key, because
    /// <c>LessonVersions</c> is a content table with row-level security while this is strict tenant
    /// data: a foreign key between them is checked with the referencing row's privileges and would
    /// turn an invisible-but-present row into a confusing constraint violation. <c>ExerciseId</c>
    /// has never had one either, for the same family of reasons.
    /// </para>
    /// </summary>
    public Guid? LessonVersionId { get; set; }

    /// <summary>
    /// The exercise the answer belongs to. Since 40.16 this is read as the exercise's identity
    /// <em>within</em> <see cref="LessonVersionId"/>'s snapshot — the <c>exerciseId</c> key inside
    /// the frozen content — and not as a pointer into the mutable <c>Exercises</c> table. The value
    /// is the same Guid; what changed is which of the two things it is trusted to mean.
    /// </summary>
    public Guid ExerciseId { get; set; }

    public string SerializedAnswer { get; set; } = "{}";
    public bool IsCorrect { get; set; }
    public int Score { get; set; }
    public string? SerializedAiFeedback { get; set; }
    public DateTime AttemptedAt { get; set; }
}
