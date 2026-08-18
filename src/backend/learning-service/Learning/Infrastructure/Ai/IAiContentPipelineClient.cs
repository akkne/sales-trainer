using Sellevate.Learning.Features.ContentGeneration.Models;

namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// Phase 40.27. learning-service's side of the two LLM calls the admin content pipeline makes. The
/// second synchronous learning → ai seam, alongside <c>IAiEvaluationClient</c>
/// (docs/TENANCY/BACKGROUND_JOBS.md §4e lists the service-to-service calls).
///
/// <para>
/// <b>Both calls are minutes-scale and neither is made inside a database transaction.</b> The caller
/// commits its claim first, calls, then opens a second transaction to write the result — see
/// <c>ContentGenerationJob.ClaimedAt</c>.
/// </para>
/// </summary>
public interface IAiContentPipelineClient
{
    /// <summary>
    /// Phase 40.28: returns the sufficiency verdict alongside the structure. One call, two answers —
    /// the model reads the material once and both questions are about that reading.
    /// </summary>
    Task<AiStructuredMaterial> StructureAsync(
        AiStructureMaterialRequest request,
        CancellationToken cancellationToken = default);

    Task<AiGeneratedLesson> GenerateAsync(
        AiGenerateExercisesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 40.32. One exercise rewritten into the organization's product and voice. Called once per
    /// exercise rather than once per batch — the unit of payment, the unit of failure and the unit a
    /// person accepts have to be the same row (docs/CONTENT_PIPELINE.md §6).
    /// </summary>
    Task<AiRewrittenExercise> RewriteExerciseAsync(
        AiAdaptExerciseRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 40.32. What is methodically wrong with one exercise a human wrote, as a list of codes.
    /// Returns nothing to apply — the reviewer never repairs, because a model that both diagnoses and
    /// silently fixes is a model nobody checks.
    /// </summary>
    Task<AiExerciseReview> ReviewExerciseAsync(
        AiAdaptExerciseRequest request,
        CancellationToken cancellationToken = default);
}
