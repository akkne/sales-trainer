using Sellevate.Learning.Features.ContentGeneration.Models;

namespace Sellevate.Learning.Features.ContentGeneration.Services.Abstract;

/// <summary>
/// Phase 40.27. Everything a person does to a pipeline run. Every method runs inside one
/// administrator's request, with their organization in <c>ITenantContext</c>; the two LLM halves are
/// a background worker's business and appear nowhere here.
///
/// <para>
/// The one transition a worker cannot make is <see cref="ApproveAsync"/>. That is the block.
/// </para>
/// </summary>
public interface IContentGenerationJobService
{
    Task<IReadOnlyList<ContentGenerationJobSummaryDto>> GetJobsAsync(
        string? status,
        CancellationToken cancellationToken = default);

    Task<ContentGenerationJobDto?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<ContentGenerationJobDto> StartAsync(
        StartContentGenerationRequestDto request,
        Guid? actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The reviewer's edit — «что убрать, что добавить». Allowed only while the run is at the
    /// checkpoint: after approval the structure is what generation was told, and rewriting it
    /// afterwards would leave a lesson whose stated source never produced it.
    /// </summary>
    Task<ContentGenerationJobDto?> UpdateStructureAsync(
        Guid jobId,
        ContentStructureDto structure,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// «Всё верно» — the only door into the expensive half.
    ///
    /// <para>
    /// <b>Idempotent by state, not by a guard flag.</b> Approving a run that is already generating or
    /// already finished returns it unchanged rather than re-queueing it: a double-clicked button and
    /// a retried request must not buy two lessons.
    /// </para>
    /// </summary>
    Task<ContentGenerationJobDto?> ApproveAsync(
        Guid jobId,
        Guid? actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts a failed run back into the half it failed in — structuring if it never produced a
    /// structure, generation if it was approved and never produced a lesson. A retry never re-pays
    /// for a half that succeeded.
    /// </summary>
    Task<ContentGenerationJobDto?> RetryAsync(Guid jobId, CancellationToken cancellationToken = default);
}
