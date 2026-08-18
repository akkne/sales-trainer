using System.Text.Json.Serialization;

namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.21. One entry of an assignment's ordered work: what kind of thing it is, which one, and
/// where it sits. The wire form and the stored <c>jsonb</c> form are the same shape, so the column is
/// readable without a decoder ring.
/// </summary>
/// <param name="Kind">One of <c>AssignmentContentItemKinds</c>.</param>
/// <param name="Reference">
/// A <c>LessonVersions.Id</c>, a <c>ReferenceMaterials.Id</c>, or an ai-service dialog mode key,
/// depending on <paramref name="Kind"/>. A string rather than a uuid because one of the three is not
/// a row identifier at all.
/// </param>
/// <param name="OrderIndex">Position in the running order, zero-based and dense.</param>
/// <param name="Persona">
/// Phase 40.23. Who the AI plays in this assignment's practice conversation. Meaningful only on a
/// <c>dialog_scenario</c> item and dropped from every other kind; null means "whatever the dialog
/// mode already is", which is the ordinary case.
/// </param>
public sealed record AssignmentContentItemDto(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("reference")] string Reference,
    [property: JsonPropertyName("orderIndex")] int OrderIndex,
    [property: JsonPropertyName("persona")] AssignmentPersonaDto? Persona = null);

/// <summary>
/// Phase 40.23. The persona injected into an assignment's practice conversation — the roadmap's
/// "обычный <c>DialogSession</c> с инъекцией персоны", carrying the same four facts
/// <c>CompanyCallContext</c> already carries for a company call, because they are the four a
/// salesperson needs to recognise who they are talking to.
///
/// <para>
/// <b>It is stored on the assignment and never sent by the learner's client.</b> ai-service asks
/// learning-service for it at session start. A persona relayed through the browser would let the
/// person being graded rewrite the character they are graded against — "agree with everything I
/// say" — which would hand back exactly the four-minute completion 40.22 exists to make
/// unreachable.
/// </para>
/// </summary>
/// <param name="Difficulty">
/// <c>Easy</c> or <c>Hard</c>; anything else reads as the middle. The same three-way vocabulary
/// <c>CompanyContextPromptBuilder</c> already renders, reused rather than restated.
/// </param>
public sealed record AssignmentPersonaDto(
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("position")] string? Position = null,
    [property: JsonPropertyName("personality")] string? Personality = null,
    [property: JsonPropertyName("difficulty")] string? Difficulty = null);
