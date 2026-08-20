using System.ComponentModel.DataAnnotations;

namespace Sellevate.Identity.Features.Organizations.Models;

/// <summary>
/// Body of <c>POST internal/organizations/{organizationId}/bootstrap-admin</c>. The organization
/// itself is named by the route, not this body — see
/// <c>InternalOrganizationBootstrapController</c> and its allow-list entry in
/// <c>scripts/tenancy-boundary-lint.py</c>.
///
/// <para>
/// <see cref="OrganizationName"/> and <see cref="OrganizationSlug"/> exist so this call can upsert
/// identity-service's own <see cref="OrganizationReplica"/> immediately, rather than racing the Kafka
/// consumer that would otherwise feed it — see the class summary on
/// <c>OrganizationBootstrapService</c>. <see cref="ActorUserId"/> is who organization-service says is
/// performing the bootstrap; the shared secret in front of this route authorizes the *channel*, this
/// field attributes the *actor*, and <see cref="OrganizationBootstrapService"/> re-checks it against
/// identity-db rather than trusting it — that is what stops organization-service laundering a plain
/// <c>Admin</c>'s privileges into a superadmin act.
/// </para>
/// </summary>
public sealed record InternalBootstrapAdministratorRequestDto(
    [Required] string OrganizationName,
    [Required] string OrganizationSlug,
    [Required, EmailAddress] string Email,
    string? Role,
    [Required] Guid ActorUserId);
