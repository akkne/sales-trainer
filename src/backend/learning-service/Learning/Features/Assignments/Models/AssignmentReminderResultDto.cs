namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.23. What the "remind" button did.
/// </summary>
/// <param name="NotifiedCount">
/// How many people were nudged. Zero is a real and useful answer: with the default scope it means
/// the whole team is done. Since 40.26 it counts only people who still hold an active membership —
/// a progress row outlives employment, and an ex-employee is not somebody to nudge.
/// </param>
/// <param name="Scope">
/// Phase 40.26. Which set was nudged, echoed back from <c>AssignmentReminderScopes</c>. It travels
/// because the digest's action link chooses a scope and the screen that follows the link has to be
/// able to say which of the two it just performed — "5 напоминаний отправлено" without saying to
/// whom is the report this block replaces, in miniature.
/// </param>
public sealed record AssignmentReminderResultDto(int NotifiedCount, string Scope);
