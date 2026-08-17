using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Learning.Features.Lessons.Models;

public sealed class UserLessonProgress : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>
    /// Phase 40.10. Owning organization; never null. The security boundary is the Postgres
    /// row-level-security policy created by the AddOrganizationId migration — the EF query
    /// filter on this property is convenience (docs/TENANCY/TENANCY.md 1.4-1.5).
    /// </summary>
    public Guid OrganizationId { get; set; }

    public Guid UserId { get; set; }
    public Guid LessonId { get; set; }

    /// <summary>
    /// Phase 40.16. The <see cref="LessonVersion"/> this row's <see cref="BestScore"/> and
    /// <see cref="CompletedAt"/> were achieved against (docs/TENANCY/CONTENT_MODEL.md §2.3).
    ///
    /// <para>
    /// Written when the row is created and refreshed only when the row actually advances — a new
    /// best score or the transition to completed. Answering an exercise again without beating the
    /// previous best leaves the version alone, because the recorded facts still belong to the older
    /// snapshot: "completed version 1" must not silently become "completed version 3" after a
    /// breaking edit the learner never saw.
    /// </para>
    ///
    /// <para>
    /// Nullable and not a foreign key, for the same two reasons as
    /// <see cref="UserExerciseAttempt.LessonVersionId"/>.
    /// </para>
    /// </summary>
    public Guid? LessonVersionId { get; set; }

    public string Status { get; set; } = "locked";
    public int BestScore { get; set; }
    public DateTime? CompletedAt { get; set; }
}
