namespace Sellevate.Organization.Features.Organizations.Models;

/// <summary>One entry of the profile's <c>objections jsonb</c> array (CONTENT_MODEL.md §3).</summary>
public sealed record OrganizationObjectionDto(string Text, string? Frequency, string? BestResponse);
