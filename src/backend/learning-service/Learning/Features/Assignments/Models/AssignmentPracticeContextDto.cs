namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.23. What ai-service needs to know to turn an ordinary practice conversation into "this
/// person's assignment", asked for at session start (docs/TENANCY/ASSIGNMENTS.md §6).
///
/// <para>
/// <b>ai-service asks; the browser never tells.</b> The learner's client could have carried this in
/// the start-session body and it would have been one fewer service call — and it would have let the
/// person being graded rewrite the character they are graded against. The bar in 40.22 is only
/// unfakeable if the thing being measured is not editable by the person measured.
/// </para>
/// </summary>
/// <param name="AssignmentId">Which assignment this session counts towards, for logging and support.</param>
/// <param name="Title">The assignment's title, so the persona knows what this rehearsal is for.</param>
/// <param name="Goal">The РОП's own words about what the team should get better at. May be null.</param>
public sealed record AssignmentPracticeContextDto(
    Guid AssignmentId,
    string Title,
    string? Goal,
    string? PersonaName,
    string? PersonaPosition,
    string? PersonaPersonality,
    string? PersonaDifficulty);
