namespace Sellevate.Organization.Eventing;

/// <summary>
/// Published when a new organization is created in the tenant registry. Consumed by
/// identity-service in Phase 40.6+ once memberships exist (the first membership — the invited
/// OrgAdmin — is created in response to this in Phase 40.9).
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
