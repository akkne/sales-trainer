namespace Sellevate.Organization.Features.Organizations.Models;

/// <summary>
/// The current caller's own organization profile. No organization id field: the caller already
/// knows which organization it read from (the gateway-validated header it sent), and the
/// boundary rule is that the id is never echoed as something a client could later replay as
/// input (docs/TENANCY/TENANCY.md §1.3).
/// </summary>
public sealed record OrganizationProfileDto(
    string? Product,
    string? Icp,
    IReadOnlyList<OrganizationObjectionDto> Objections,
    IReadOnlyList<string> ScriptStages,
    string? Tone,
    IReadOnlyDictionary<string, string> Glossary,
    IReadOnlyList<string> BannedClaims,
    DateTime CreatedAt,
    DateTime UpdatedAt);
