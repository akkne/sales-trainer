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
    Task<ContentStructureDto> StructureAsync(
        AiStructureMaterialRequest request,
        CancellationToken cancellationToken = default);

    Task<AiGeneratedLesson> GenerateAsync(
        AiGenerateExercisesRequest request,
        CancellationToken cancellationToken = default);
}
