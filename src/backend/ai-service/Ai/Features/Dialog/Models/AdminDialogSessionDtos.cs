namespace Sellevate.Ai.Features.Dialog.Models;

/// <summary>
/// Phase 40.25. One graded conversation as the РОП's list shows it — enough to decide whether it is
/// worth opening, and nothing that requires loading a transcript
/// (docs/TENANCY/ASSIGNMENTS.md §4).
///
/// <para>
/// It carries <see cref="UserId"/>, which the learner's own <c>DialogSessionSummaryDto</c> does not:
/// that DTO answers "my history" and never needs to say whose. This one is a list across a team and
/// is useless without it. Names are deliberately absent — ai-service holds no user replica, and the
/// screen already knows the team's names from the same place it drew the heat map.
/// </para>
/// </summary>
public sealed record AdminDialogSessionSummaryDto(
    string Id,
    Guid UserId,
    Guid BundleId,
    Guid ModeId,
    string? ModeKey,
    string? ModeTitle,
    string Status,
    int MessageCount,
    int? Score,
    string? FeedbackSummary,
    Guid? AssignmentId,
    DateTime CreatedAt,
    DateTime? CompletedAt);

/// <summary>
/// Phase 40.25. One conversation in full: the transcript the РОП selects three lines out of, plus
/// the grade those lines are being argued about.
///
/// <para>
/// <b>Message index is part of the contract.</b> A quoted fragment has to be citable after the fact,
/// and a quote that names only its text cannot survive the same sentence being said twice. The РОП's
/// comment and the manager's dispute (learning-service, 40.25) both reference a session id and a
/// message index, which is why the index is returned explicitly rather than left implicit in array
/// order.
/// </para>
/// </summary>
public sealed record AdminDialogTranscriptDto(
    string Id,
    Guid UserId,
    Guid BundleId,
    Guid ModeId,
    string? ModeKey,
    string? ModeTitle,
    string Status,
    int? Score,
    DialogFeedbackDto? Feedback,
    Guid? AssignmentId,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    IReadOnlyList<AdminDialogTranscriptMessageDto> Messages);

/// <summary>Phase 40.25. One line of a transcript, with the index a quote refers to it by.</summary>
public sealed record AdminDialogTranscriptMessageDto(
    int Index,
    string Role,
    string Content,
    DateTime Timestamp);
