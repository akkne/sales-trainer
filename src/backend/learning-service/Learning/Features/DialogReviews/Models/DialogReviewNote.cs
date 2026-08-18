using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;

namespace Sellevate.Learning.Features.DialogReviews.Models;

/// <summary>
/// Phase 40.25. One annotation on one practice conversation: either the РОП coaching a manager on a
/// fragment of it, or the manager saying the AI graded them wrongly
/// (docs/TENANCY/ASSIGNMENTS.md §4.1).
///
/// <para>
/// <b>It lives in learning-service, not in ai-service, and that is the block's main structural
/// choice.</b> The conversation is a Mongo document in ai-service, so the obvious home looks like
/// ai-service. Three things say otherwise. The disputed number is <c>UserDialogScore</c>,
/// which is a learning-db row and the value that actually drives an assignment's threshold — a
/// dispute about a grade the РОП's screen never uses would be a dispute about nothing. The РОП's
/// review queue belongs on the РОП's dashboard, which is here. And these are strict tenant rows
/// under a Postgres row-level-security policy, whereas ai-service's session store has no such net
/// and its Postgres holds only content tables. Alternatives are recorded in docs/DECISIONS.md
/// (2026-08-18).
/// </para>
///
/// <para>
/// <b>Nothing here reads ai-service.</b> The session id, the manager, the scenario and the grade all
/// come from the <c>UserDialogScores</c> row 40.22 already mirrors on <c>dialog.evaluated</c>, so a
/// conversation with no recorded score cannot be annotated at all — which is the right refusal: an
/// ungraded conversation has no grade to dispute and is not on the screen the fragment was selected
/// from.
/// </para>
/// </summary>
public sealed class DialogReviewNote : ITenantScoped
{
    public Guid Id { get; set; }

    /// <summary>
    /// Owning organization; never null. The boundary is the row-level-security policy created by the
    /// AddDialogReviewNotes migration — plain equality, because a conversation and everything said
    /// about it happen inside one organization and there is no global counterpart
    /// (docs/TENANCY/TENANCY.md §1.4–1.5).
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>One of <see cref="DialogReviewKinds"/>. Immutable once written.</summary>
    public string Kind { get; set; } = DialogReviewKinds.CoachingNote;

    /// <summary>
    /// ai-service's session identifier, a string because that is what Mongo assigns and what
    /// this row's source — <c>UserDialogScores.SessionId</c> — already stores. Never a foreign key: the conversation lives in another service's database.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// The scenario, copied from the score row. Denormalized so that a prompt-tuning dataset is one
    /// query over this table rather than a join against a second service — the mode key is the thing
    /// a grading prompt is attached to, and it is what makes "which prompts do managers argue with"
    /// answerable at all.
    /// </summary>
    public string DialogModeKey { get; set; } = string.Empty;

    /// <summary>Whose conversation it is — the manager. Resolved from the score row, never from the request.</summary>
    public Guid SubjectUserId { get; set; }

    /// <summary>
    /// Who wrote this row: the РОП for a coaching note, the manager themselves for a dispute. Equal
    /// to <see cref="SubjectUserId"/> exactly when the kind is a dispute.
    /// </summary>
    public Guid AuthorUserId { get; set; }

    /// <summary>
    /// First message of the quoted fragment, as an index into the transcript, or null when the note
    /// is about the conversation as a whole.
    /// </summary>
    public int? QuotedFromMessageIndex { get; set; }

    /// <summary>Last message of the quoted fragment, inclusive. Never smaller than the first.</summary>
    public int? QuotedToMessageIndex { get; set; }

    /// <summary>
    /// A frozen copy of the quoted lines.
    ///
    /// <para>
    /// <b>A copy, even though the transcript is immutable and one service away.</b> The РОП's whole
    /// use for this is «три реплики где менеджер слил цену» on Monday morning, and a note that
    /// renders as three empty lines because ai-service is slow, or because retention eventually
    /// trims old sessions, is a note that failed at the only moment it mattered. The indexes stay
    /// alongside so the fragment can still be located in context when the session is there.
    /// </para>
    /// </summary>
    public string? QuotedText { get; set; }

    /// <summary>The РОП's coaching, or the manager's reason for disputing. Never blank.</summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// The 0–100 grade being argued about, frozen at the moment the row was written. Frozen rather
    /// than read live because the point of the row is what the machine said <i>then</i>: a dataset
    /// built from a number that can move is a dataset of unprovable claims, which is 40.16's whole
    /// argument in a different table.
    /// </summary>
    public int? DisputedScore { get; set; }

    /// <summary>One of <see cref="DialogReviewStatuses"/>. Every row is created <c>open</c>.</summary>
    public string Status { get; set; } = DialogReviewStatuses.Open;

    /// <summary>
    /// The РОП's verdict in their own words, or null while the row is open. Required when a dispute
    /// is rejected: "the grade stands, because" is the sentence that keeps the mechanism from being
    /// a rubber stamp.
    /// </summary>
    public string? Resolution { get; set; }

    /// <summary>
    /// What the РОП thinks the grade should have been, 0–100, set only when a dispute is upheld.
    ///
    /// <para>
    /// <b>It does not change <c>UserDialogScores</c> and therefore does not change any
    /// assignment verdict.</b> 40.22 made every progress number derived from attempt rows and
    /// recomputed on every event; a hand-edited score would be overwritten by the next redelivery
    /// and, worse, would make the completion threshold negotiable by the person being measured. The
    /// label is recorded here for the humans and for the dataset — retro-scoring is a decision the
    /// owner has not made, and it is in docs/DONT_FORGET.md rather than guessed at.
    /// </para>
    /// </summary>
    public int? AdjustedScore { get; set; }

    /// <summary>Who closed it: the manager for a note, the РОП for a dispute.</summary>
    public Guid? ResolvedBy { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
