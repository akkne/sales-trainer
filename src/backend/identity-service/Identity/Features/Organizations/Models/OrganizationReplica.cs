namespace Sellevate.Identity.Features.Organizations.Models;

/// <summary>
/// identity-service's read-only projection of the tenant registry owned by organization-service
/// (Phase 40.9). The same pattern as <c>BuildingBlocks/Identity/UserReplica.cs</c>: a bare
/// <c>uuid</c> key, no foreign key, kept up to date over Kafka
/// (<c>organization.created</c> / <c>organization.updated</c> / <c>organization.suspended</c>) —
/// see docs/TENANCY/TENANCY.md §1.1.
///
/// <para>
/// It exists because identity-service is the only service that mints tokens, and a suspended
/// organization has to stop producing them. Asking organization-service synchronously on every
/// login would put a second service on the authentication hot path and make identity unable to
/// sign anyone in whenever organization-service is down.
/// </para>
///
/// <para>
/// Deliberately **not** <c>ITenantScoped</c> and without a row-level-security policy, for the same
/// reason as <c>OrganizationAuthConfiguration</c>: it is read before authentication has produced a
/// tenant context, and "which organization is this" is a cross-tenant question by nature.
/// </para>
/// </summary>
public sealed class OrganizationReplica
{
    /// <summary>Primary key — the registry id minted by organization-service.</summary>
    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public OrganizationReplicaStatus Status { get; set; } = OrganizationReplicaStatus.Active;

    public DateTime UpdatedAt { get; set; }
}
