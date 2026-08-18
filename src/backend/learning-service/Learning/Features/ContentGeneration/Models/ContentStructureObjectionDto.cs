namespace Sellevate.Learning.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.27. One objection in the extracted structure, in the shape
/// <c>OrganizationObjectionSnapshot</c> uses (docs/TENANCY/CONTENT_MODEL.md §3), so that promoting a
/// reviewed structure into the organization profile is a copy rather than a translation — see
/// docs/DECISIONS.md (2026-08-18) and roadmap 40.29.
/// </summary>
public sealed record ContentStructureObjectionDto(string Text, string? BestResponse);
