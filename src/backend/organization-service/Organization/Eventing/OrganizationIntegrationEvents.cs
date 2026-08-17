namespace Sellevate.Organization.Eventing;

/// <summary>
/// Published when a new organization is created in the tenant registry. Consumed by
/// identity-service in Phase 40.6+ once memberships exist (the first membership — the invited
/// TenancySuperAdmin — is created in response to this in Phase 40.9).
/// </summary>
public sealed record OrganizationCreatedEvent(Guid OrganizationId, string Name, string Slug);

/// <summary>
/// Published when an organization's registry row changes: name/slug edits and reactivation
/// both go through this event rather than minting a fourth topic — see docs/DECISIONS.md.
/// </summary>
public sealed record OrganizationUpdatedEvent(Guid OrganizationId, string Name, string Slug, string Status);

/// <summary>
/// Published when an organization is suspended by the platform. Tenant-scoped services are
/// expected to react by rejecting further writes for the organization (Phase 40.9+ scope;
/// no consumer exists yet).
/// </summary>
public sealed record OrganizationSuspendedEvent(Guid OrganizationId, string Name);

/// <summary>
/// Published when an organization's content-substitution profile is saved (Phase 40.19). The
/// payload is the whole profile rather than a delta, because its consumers are last-writer-wins
/// replicas: a delta would make a dropped message permanent, while a full snapshot makes the next
/// save repair it.
///
/// <para>
/// The jsonb columns travel as their raw JSON text. Re-modelling objections, script, glossary and
/// banned claims into three services' worth of DTOs would give every service its own chance to
/// disagree about the shape; <c>OrganizationProfileSnapshot</c> in BuildingBlocks parses them once,
/// the same way, for everybody.
/// </para>
/// </summary>
public sealed record OrganizationProfileUpdatedEvent(
    Guid OrganizationId,
    string? Product,
    string? Icp,
    string? Tone,
    string ObjectionsJson,
    string ScriptJson,
    string GlossaryJson,
    string BannedClaimsJson,
    DateTime UpdatedAt);
