namespace Sellevate.Identity.Eventing;

/// <summary>
/// Consumer-side mirrors of the payloads organization-service publishes
/// (<c>Sellevate.Organization.Eventing.OrganizationIntegrationEvents</c>). Duplicated rather than
/// shared because the two services own separate assemblies and databases and must deploy
/// independently — the contract is the JSON on the topic, not a .NET type. Same reasoning as the
/// per-service copies of the <c>user.*</c> payloads.
/// </summary>
public sealed record OrganizationCreatedEvent(Guid OrganizationId, string Name, string Slug);

public sealed record OrganizationUpdatedEvent(Guid OrganizationId, string Name, string Slug, string Status);

public sealed record OrganizationSuspendedEvent(Guid OrganizationId, string Name);
