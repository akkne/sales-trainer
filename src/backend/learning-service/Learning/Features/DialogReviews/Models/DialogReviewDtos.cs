using System.ComponentModel.DataAnnotations;

namespace Sellevate.Learning.Features.DialogReviews.Models;

/// <summary>
/// Phase 40.25. One review note as either side reads it (docs/TENANCY/ASSIGNMENTS.md §4.1).
///
/// <para>
/// The same shape for the РОП's queue and the manager's inbox on purpose. The two screens differ in
/// which rows they are shown and which button they get, not in what a row is — and one DTO is what
/// keeps a dispute from looking like a different object depending on who is reading it.
/// </para>
/// </summary>
public sealed record DialogReviewNoteDto(
    Guid Id,
    string Kind,
    string Status,
    string SessionId,
    string DialogModeKey,
    Guid SubjectUserId,
    string? SubjectDisplayName,
    Guid AuthorUserId,
    string? AuthorDisplayName,
    int? QuotedFromMessageIndex,
    int? QuotedToMessageIndex,
    string? QuotedText,
    string Comment,
    int? DisputedScore,
    string? Resolution,
    int? AdjustedScore,
    Guid? ResolvedBy,
    DateTime? ResolvedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Phase 40.25. The РОП selects a fragment of a conversation and comments on it.
///
/// <para>
/// <b>The request names a conversation and nothing else about who it belongs to.</b> The manager,
/// the scenario and the grade are read from the <c>UserDialogScores</c> row for that session inside
/// the caller's organization — so a hand-written body cannot address a note at somebody else's
/// employee, and the tenancy rule that the organization never arrives in a payload keeps holding
/// without a second check.
/// </para>
/// </summary>
/// <param name="QuotedText">
/// The lines themselves. Required, because a coaching note whose whole content is "messages 4 to 6"
/// is a note the РОП cannot re-read next month and the manager cannot read at all if the session has
/// aged out.
/// </param>
public sealed record CreateCoachingNoteRequestDto(
    [property: Required] string? SessionId,
    int? QuotedFromMessageIndex,
    int? QuotedToMessageIndex,
    [property: Required] string? QuotedText,
    [property: Required] string? Comment);

/// <summary>
/// Phase 40.25. The manager says the grade was wrong.
///
/// <para>
/// A quoted fragment is optional here and required on a coaching note, which is the asymmetry the
/// two directions actually have: the РОП is pointing at something specific, while the manager is
/// usually arguing about the conversation as a whole. Demanding a fragment would put a step between
/// a person who feels wronged and the mechanism that exists to keep them trusting the numbers.
/// </para>
/// </summary>
public sealed record CreateScoreDisputeRequestDto(
    [property: Required] string? SessionId,
    int? QuotedFromMessageIndex,
    int? QuotedToMessageIndex,
    string? QuotedText,
    [property: Required] string? Comment);

/// <summary>
/// Phase 40.25. The РОП's verdict on a dispute.
/// </summary>
/// <param name="Outcome">
/// <c>upheld</c> or <c>rejected</c> — the two values of <c>DialogReviewStatuses</c> that end a
/// dispute. Spelled as the outcome rather than as a boolean so the wire format says what it means
/// and matches the row it writes.
/// </param>
/// <param name="Resolution">
/// The РОП's words. Required on a rejection: closing somebody's complaint with silence is how the
/// mechanism becomes a rubber stamp, which is the failure §4.1 is written against.
/// </param>
/// <param name="AdjustedScore">
/// What the grade should have been, 0–100, allowed only on an upheld dispute. Recorded, never
/// applied — see <c>DialogReviewNote.AdjustedScore</c>.
/// </param>
public sealed record ResolveScoreDisputeRequestDto(
    [property: Required] string? Outcome,
    string? Resolution,
    int? AdjustedScore);

/// <summary>Phase 40.25. Why a write was refused, in the caller's terms.</summary>
public sealed class DialogReviewValidationException(string message) : Exception(message);
