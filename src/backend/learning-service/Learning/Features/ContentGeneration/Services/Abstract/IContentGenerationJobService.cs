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
    /// Phase 40.28. «Вот ещё материал» — the answer to a refusal, and the only way out of the
    /// <c>insufficient</c> state that does not involve typing the structure by hand.
    ///
    /// <para>
    /// The text is appended and the run resumes where it stopped. It never restarts: the next
    /// structuring call reads only the added part, alongside the structure already extracted, so
    /// arguing with a refusal costs the price of what was added and not the price of the deck again.
    /// </para>
    /// </summary>
    Task<ContentGenerationJobDto?> SupplementMaterialAsync(
        Guid jobId,
        SupplementContentMaterialRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The reviewer's edit — «что убрать, что добавить». Allowed at the checkpoint and, since 40.28,
    /// on a refused run: somebody who knows the four objections the material lacked may simply type
    /// them, and the edited structure is re-inspected rather than taken on trust. Never after
    /// approval — the structure is by then what generation was told, and rewriting it would leave a
    /// lesson whose stated source never produced it.
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
    ///
    /// <para>
    /// Phase 40.28: it is also the last sufficiency gate. The structure is re-inspected here rather
    /// than trusted to have been inspected when it was written, because between the write and the
    /// approval there is a network and a stale screen. A structure that does not pass moves the run
    /// to <c>insufficient</c> and raises
    /// <see cref="Models.ContentGenerationInsufficientMaterialException"/>.
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
