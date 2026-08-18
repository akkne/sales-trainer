namespace Sellevate.Organization.Features.Organizations.Models;

/// <summary>
/// Phase 40.29. One objection inside an extracted draft, in the shape learning-service's
/// <c>ContentStructureObjectionDto</c> already has.
///
/// <para>
/// It carries no <c>Frequency</c>, and that is not an omission. Nothing reads how often an objection
/// comes up out of a product deck, and a model asked for it invents it. The profile's own
/// <see cref="OrganizationObjectionDto"/> keeps the field for the human who fills it in by hand, and
/// the merger preserves it on entries that already have it.
/// </para>
/// </summary>
public sealed record ExtractedProfileObjectionDto(string Text, string? BestResponse);
