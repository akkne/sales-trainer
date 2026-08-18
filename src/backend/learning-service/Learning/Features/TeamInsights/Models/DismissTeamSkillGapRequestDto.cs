namespace Sellevate.Learning.Features.TeamInsights.Models;

/// <summary>
/// Phase 40.31. «Не сейчас» — the body of a dismissal.
///
/// <para>
/// Nothing about the stage or the measurement is in it. Both are read server-side from the same
/// computation that drew the panel, the property 40.25 built <c>DialogReviewNotes</c> around: a
/// caller cannot dismiss a gap that is not currently a gap, and cannot record a number the team did
/// not actually score.
/// </para>
/// </summary>
/// <param name="Note">Why, in the РОП's own words. Optional and never shown to the team.</param>
public sealed record DismissTeamSkillGapRequestDto(string? Note);
