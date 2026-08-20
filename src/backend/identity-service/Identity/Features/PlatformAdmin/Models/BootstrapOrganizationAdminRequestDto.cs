using System.ComponentModel.DataAnnotations;

namespace Sellevate.Identity.Features.PlatformAdmin.Models;

/// <summary>
/// Body of <c>POST /admin/platform/organizations/bootstrap-admin</c> — invites the first
/// administrator of a brand new organization.
///
/// <para>
/// Until 2026-08-20 there was deliberately no role field at all: the endpoint could only ever
/// issue a <c>TenancySuperAdmin</c> invite, because a role taken from the request would let a
/// platform-only endpoint mint any organization role anywhere — a far larger blast radius than
/// the one thing it exists to do. The owner asked for the choice back, so the restriction is
/// loosened rather than removed: <see cref="Role"/> is parsed by the same rules
/// <c>InviteService.ParseRole</c> already applies to an ordinary invite, and
/// <c>PlatformAdminService</c> then refuses anything except <c>TenancyAdmin</c> and
/// <c>TenancySuperAdmin</c> — <c>Manager</c> included — with a <c>400</c>. The endpoint still
/// cannot mint an arbitrary organization role; it can only pick which rank of administrator it
/// bootstraps. Omitted or blank defaults to <c>TenancySuperAdmin</c>, so every caller that predates
/// this field keeps working unchanged.
/// </para>
///
/// <para>
/// Carries an organization id for the same reason as
/// <see cref="CreateImpersonationRequestDto"/> — see that type and the allow-list in
/// <c>scripts/tenancy-boundary-lint.py</c>. The endpoint refuses any organization that already has
/// an active <c>TenancyAdmin</c> or <c>TenancySuperAdmin</c>, or a pending invite for either, so it
/// cannot be used as a back door into a running customer's organization.
/// </para>
/// </summary>
public sealed record BootstrapOrganizationAdminRequestDto(
    [Required] Guid OrganizationId,
    [Required, EmailAddress, MaxLength(320)] string Email,
    string? Role = null);
