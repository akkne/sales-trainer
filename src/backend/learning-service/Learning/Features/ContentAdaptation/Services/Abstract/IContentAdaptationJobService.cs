using Sellevate.Learning.Features.ContentAdaptation.Models;

namespace Sellevate.Learning.Features.ContentAdaptation.Services.Abstract;

/// <summary>
/// Phase 40.32. The human half of batch adaptation: start a batch, read its queue, and answer one
/// item at a time.
///
/// <para>
/// <b>There is no "accept all".</b> Not as an oversight and not as a later feature: the roadmap's
/// «принять/отклонить поштучно, никогда не автоприменение» is the whole point of the block, and a
/// bulk verb would be auto-apply with a person's name attached to it. Sixty items is sixty
/// decisions, and if that is too many the answer is a smaller stage — which is why the ceiling on
/// batch size exists.
/// </para>
/// </summary>
public interface IContentAdaptationJobService
{
    Task<IReadOnlyList<ContentAdaptationJobSummaryDto>> GetJobsAsync(
        string? mode,
        string? status,
        CancellationToken cancellationToken = default);

    Task<ContentAdaptationJobDto?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// One item with both documents and the change list. Returns <see langword="null"/> when the item
    /// is not part of a batch this organization owns.
    /// </summary>
    Task<ContentAdaptationItemDto?> GetItemAsync(
        Guid jobId,
        Guid itemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the batch and its items. Spends nothing — the scope is a database query and every LLM
    /// call happens later, in the worker, one item at a time.
    /// </summary>
    Task<ContentAdaptationJobDto> StartAsync(
        StartContentAdaptationRequestDto request,
        Guid? actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// «Применить это предложение.» The only code path in the block that writes an
    /// <c>Exercise</c>, and it runs inside an administrator's request. Forks the lesson first when
    /// the exercise belongs to the global library (40.18 copy-on-write), refuses when the exercise
    /// has moved since the proposal was computed, and refuses outright in review mode — a finding is
    /// not a patch.
    /// </summary>
    Task<ContentAdaptationItemDto?> AcceptItemAsync(
        Guid jobId,
        Guid itemId,
        Guid? actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// «Нет.» Resolves the item without touching content — the answer that costs nothing and is
    /// therefore the one the screen must make as easy as accepting.
    /// </summary>
    Task<ContentAdaptationItemDto?> RejectItemAsync(
        Guid jobId,
        Guid itemId,
        Guid? actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-queues the items that burned their attempts, and only those. A batch that produced fifty
    /// good proposals and lost ten must not re-pay for the fifty.
    /// </summary>
    Task<ContentAdaptationJobDto?> RetryAsync(Guid jobId, CancellationToken cancellationToken = default);
}
