namespace Sellevate.Organization.Features.Organizations.Models;

/// <summary>
/// Upserts the caller's own organization profile. No organization id field — the target
/// organization is resolved solely from <c>ITenantContext</c> (the gateway-validated
/// <c>X-Organization-Id</c> header), never from the request body (docs/TENANCY/TENANCY.md §1.3).
/// </summary>
public sealed record UpdateOrganizationProfileRequestDto(
    string? Product,
    string? Icp,
    IReadOnlyList<OrganizationObjectionDto>? Objections,
    IReadOnlyList<string>? ScriptStages,
    string? Tone,
    IReadOnlyDictionary<string, string>? Glossary,
    IReadOnlyList<string>? BannedClaims);
