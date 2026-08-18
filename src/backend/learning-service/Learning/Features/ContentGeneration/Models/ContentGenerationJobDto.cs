namespace Sellevate.Learning.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.27. One run, as the admin screen sees it — including the structure, which is the whole
/// reason the screen exists.
///
/// <para>
/// It carries no <c>organizationId</c>, like every other DTO in this codebase: the tenant comes from
/// the gateway-validated header and never from a payload (docs/TENANCY/TENANCY.md §1.3,
/// enforced by <c>scripts/tenancy-boundary-lint.py</c>).
/// </para>
/// </summary>
/// <param name="Insufficiency">
/// Phase 40.28. Why the pipeline refused and what to bring instead, or null when it did not refuse.
/// Non-null exactly when <paramref name="Status"/> is <c>insufficient</c>.
/// </param>
/// <param name="GapSourceRef">
/// Phase 40.31. The measured failure this run was started to answer
/// (<c>skill-gap:&lt;stage&gt;@&lt;date&gt;</c>), or null for a run somebody started by pasting
/// material. It is what an assignment created from this run will carry as its <c>source_ref</c>.
/// </param>
public sealed record ContentGenerationJobDto(
    Guid Id,
    string Title,
    string Status,
    string? GapSourceRef,
    string SourceMaterial,
    ContentStructureDto? Structure,
    ContentInsufficiencyDto? Insufficiency,
    DateTime? StructuredAt,
    DateTime? ApprovedAt,
    Guid? ProducedLessonId,
    Guid? ProducedLessonVersionId,
    int ProducedExerciseCount,
    DateTime? GeneratedAt,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime UpdatedAt);
