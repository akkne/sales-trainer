using Sellevate.Learning.Features.DialogReviews.Models;

namespace Sellevate.Learning.Features.DialogReviews.Services.Abstract;

/// <summary>
/// Phase 40.25. The two-way loop of docs/TENANCY/ASSIGNMENTS.md §4.1: the РОП comments on a
/// fragment, the manager disputes a grade, and each side closes the other's row.
///
/// <para>
/// Every method is bounded to the caller's organization by <c>ITenantContext</c> and none takes an
/// organization argument (docs/TENANCY/TENANCY.md §1.3). The actor is always passed explicitly,
/// because "who wrote this" and "who is allowed to close it" are the whole of this feature's
/// authorization and neither may be inferred from an ambient value.
/// </para>
/// </summary>
public interface IDialogReviewService
{
    /// <summary>
    /// The РОП sends a quoted fragment with a comment to whoever held the conversation. Raises
    /// <see cref="DialogReviewValidationException"/> when the conversation has no recorded score in
    /// this organization — which is also how it refuses a session id belonging to somebody else.
    /// </summary>
    Task<DialogReviewNoteDto> CreateCoachingNoteAsync(
        Guid authorUserId,
        CreateCoachingNoteRequestDto requestDto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The manager disputes the grade on one of their own conversations. Refuses a session that is
    /// not theirs and refuses a second open dispute on the same conversation.
    /// </summary>
    Task<DialogReviewNoteDto> CreateScoreDisputeAsync(
        Guid authorUserId,
        CreateScoreDisputeRequestDto requestDto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The РОП's verdict. Returns <see langword="null"/> when there is no such dispute in the
    /// caller's organization.
    /// </summary>
    Task<DialogReviewNoteDto?> ResolveScoreDisputeAsync(
        Guid actorUserId,
        Guid noteId,
        ResolveScoreDisputeRequestDto requestDto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The manager marks a coaching note read. Returns <see langword="null"/> when there is no such
    /// note addressed to them.
    /// </summary>
    Task<DialogReviewNoteDto?> AcknowledgeCoachingNoteAsync(
        Guid actorUserId,
        Guid noteId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The РОП's queue: everything in the organization, newest first, optionally narrowed to one
    /// kind, one status or one conversation.
    /// </summary>
    Task<IReadOnlyList<DialogReviewNoteDto>> GetForOrganizationAsync(
        string? kind,
        string? status,
        string? sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The manager's inbox: notes addressed to them and disputes they filed, newest first.
    /// </summary>
    Task<IReadOnlyList<DialogReviewNoteDto>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
