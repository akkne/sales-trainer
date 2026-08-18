using Sellevate.Learning.Features.Programs.Models;

namespace Sellevate.Learning.Features.Programs.Services.Abstract;

/// <summary>
/// Phase 40.17. The lifecycle of one organization's programme snapshots
/// (docs/TENANCY/CONTENT_MODEL.md §2.5). Every operation is bounded to the caller's organization by
/// <c>ITenantContext</c>; none of them takes an organization as an argument, and none ever will
/// (docs/TENANCY/TENANCY.md §1.3).
/// </summary>
public interface IProgramVersionService
{
    Task<IReadOnlyList<ProgramVersionSummaryDto>> GetVersionsAsync(
        CancellationToken cancellationToken = default);

    Task<ProgramVersionDto?> GetVersionAsync(
        Guid programVersionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens editing: returns the organization's single mutable draft, creating it if there is none,
    /// and re-deriving its items from the live skill tree either way.
    ///
    /// <para>
    /// Each item is pinned to the lesson's newest published version, minting a version 1 for a
    /// lesson that has never been published — the same resolver an attempt goes through
    /// (<c>ILessonVersionService.EnsurePublishedVersionIdAsync</c>), so a programme and the progress
    /// recorded against it cannot disagree about which snapshot a lesson currently is.
    /// </para>
    /// </summary>
    Task<ProgramVersionDto> EnsureDraftAsync(
        Guid? actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Freezes the open draft as a published version. Returns <see langword="null"/> when there is
    /// no draft to publish. When the draft's items are identical to the last published version's,
    /// nothing is frozen: the draft is discarded and the existing version comes back with
    /// <c>CreatedNewVersion = false</c>.
    /// </summary>
    Task<PublishProgramVersionResultDto?> PublishAsync(
        Guid? actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// What changes between two of the organization's programme versions. Returns
    /// <see langword="null"/> when either version does not exist or is not visible to the caller.
    /// </summary>
    Task<ProgramDiffDto?> GetDiffAsync(
        Guid fromProgramVersionId,
        Guid toProgramVersionId,
        CancellationToken cancellationToken = default);
}
