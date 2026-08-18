namespace Sellevate.Ai.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.27. One objection the customer's reps actually hear, as the model read it out of the
/// uploaded material.
///
/// <para>
/// The shape is <c>OrganizationObjectionSnapshot</c> from BuildingBlocks, deliberately field for
/// field: the extracted structure is the organization profile of
/// docs/TENANCY/CONTENT_MODEL.md §3 seen one step earlier, and a translation layer between the two
/// would be the place they drift apart. It is redeclared here rather than referenced because this
/// service returns it over HTTP and the wire shape of an internal endpoint should not move when a
/// shared render-path record does.
/// </para>
/// </summary>
public sealed record ExtractedObjectionDto(string Text, string? BestResponse);
