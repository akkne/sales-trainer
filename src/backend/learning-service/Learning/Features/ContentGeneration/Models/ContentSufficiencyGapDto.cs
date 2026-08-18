namespace Sellevate.Learning.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.28. One thing that is missing, and what to bring instead.
/// </summary>
/// <param name="Code">
/// One of <c>ContentSufficiencyCodes</c>. The screen keys off this, not off the sentence: a list of
/// codes can be rendered as checkboxes, sorted, counted and localised; a paragraph can only be shown.
/// </param>
/// <param name="Message">
/// The sentence the РОП reads, resolved from the code at the moment the refusal is recorded. Stored
/// alongside the code rather than resolved on read so that a run refused last month still explains
/// itself in the words it was refused with.
/// </param>
public sealed record ContentSufficiencyGapDto(string Code, string Message);
