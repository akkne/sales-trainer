using System.ComponentModel.DataAnnotations;

namespace Sellevate.Identity.Features.PlatformAdmin.Models;

/// <summary>
/// Body of <c>POST /admin/platform/organizations/bootstrap-admin</c> — invites the very first
/// <c>TenancySuperAdmin</c> of a brand new organization.
///
/// <para>
/// There is deliberately no role field: this endpoint only ever issues an <c>TenancySuperAdmin</c> invite.
/// A role taken from the request would make a platform endpoint able to mint any organization role
/// anywhere, which is a larger blast radius than the one thing it exists to do.
/// </para>
///
/// <para>
/// Carries an organization id for the same reason as
/// <see cref="CreateImpersonationRequestDto"/> — see that type and the allow-list in
/// <c>scripts/tenancy-boundary-lint.py</c>. The endpoint refuses any organization that already has
/// an active <c>TenancySuperAdmin</c> or a pending <c>TenancySuperAdmin</c> invite, so it cannot be used as a back
/// door into a running customer's organization.
/// </para>
/// </summary>
public sealed record BootstrapOrganizationAdminRequestDto(
    [Required] Guid OrganizationId,
    [Required, EmailAddress, MaxLength(320)] string Email);
