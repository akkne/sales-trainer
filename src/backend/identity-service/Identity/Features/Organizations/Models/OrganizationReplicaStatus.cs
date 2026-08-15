namespace Sellevate.Identity.Features.Organizations.Models;

/// <summary>
/// Mirrors <c>Sellevate.Organization.Features.Organizations.Models.OrganizationStatus</c>. Kept as
/// a separate enum rather than a shared one because the two services own separate databases and
/// must be able to deploy independently — the wire contract is the string on
/// <c>OrganizationUpdatedEvent.Status</c>, not a shared type.
/// </summary>
public enum OrganizationReplicaStatus
{
    Active = 0,
    Suspended = 1
}
