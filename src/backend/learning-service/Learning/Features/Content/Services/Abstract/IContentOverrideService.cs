using Sellevate.Learning.Features.Content.Models;

namespace Sellevate.Learning.Features.Content.Services.Abstract;

/// <summary>
/// Phase 40.18. Copy-on-write overrides and the staleness queue
/// (docs/TENANCY/CONTENT_MODEL.md §1, §2.6).
///
/// <para>
/// The one invariant every method here exists to protect: <b>a copy is made at the moment an
/// administrator presses "edit", and never at onboarding</b>. Cloning the curriculum into each new
/// organization is the move that looks helpful and is unrecoverable — after fifteen customers there
/// are fifteen forks, improving a base lesson reaches nobody, and every base fix becomes fifteen
/// merges by hand. Nothing in this service runs on a schedule, on a Kafka event, or on organization
/// creation; every method is the direct result of a person clicking something.
/// </para>
/// </summary>
public interface IContentOverrideService
{
    Task<ContentOverrideResult> CreateOverrideAsync(
        string kind,
        Guid baseId,
        Guid? actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The organization's overrides, newest content family first, with staleness computed against
    /// the base as it stands right now. <paramref name="staleOnly"/> narrows it to the review queue.
    /// </summary>
    Task<IReadOnlyList<ContentOverrideDto>> GetOverridesAsync(
        bool staleOnly,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One override with the documents a reviewer has to compare. Returns <see langword="null"/>
    /// when the row is not an override this organization owns.
    /// </summary>
    Task<ContentOverrideReviewDto?> GetReviewAsync(
        string kind,
        Guid overrideId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Review action one — <b>take the new base</b>. Retires the override so read resolution stops
    /// shadowing the global row; it is not deleted, because progress rows point at it without a
    /// foreign key and deleting it to tidy a queue would orphan that history.
    /// </summary>
    Task<bool> AcceptBaseAsync(
        string kind,
        Guid overrideId,
        Guid? actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Review action two — <b>keep the override</b>. Re-points the fork marker at the base as it is
    /// now, which is the entire reason 40.15 left <c>LessonVersion.BaseVersionId</c> writable on a
    /// row its freeze trigger otherwise seals. The override's content is not touched: the
    /// organization is saying "we looked, and ours still stands", not "merge them".
    /// </summary>
    Task<bool> KeepOverrideAsync(
        string kind,
        Guid overrideId,
        Guid? actorId,
        CancellationToken cancellationToken = default);
}
