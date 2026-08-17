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
public sealed record AssignmentContentItemDto(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("reference")] string Reference,
    [property: JsonPropertyName("orderIndex")] int OrderIndex);
