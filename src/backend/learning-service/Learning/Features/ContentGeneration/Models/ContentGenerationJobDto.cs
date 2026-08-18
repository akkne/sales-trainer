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
public sealed record ContentGenerationJobDto(
    Guid Id,
    string Title,
    string Status,
    string SourceMaterial,
    ContentStructureDto? Structure,
    DateTime? StructuredAt,
    DateTime? ApprovedAt,
    Guid? ProducedLessonId,
    Guid? ProducedLessonVersionId,
    int ProducedExerciseCount,
    DateTime? GeneratedAt,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime UpdatedAt);
