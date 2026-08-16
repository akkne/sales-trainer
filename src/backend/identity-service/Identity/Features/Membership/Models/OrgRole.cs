namespace Sellevate.Identity.Features.Membership.Models;

/// <summary>
/// The organization-scoped role, held on <see cref="Membership"/> — never on
/// <see cref="Sellevate.Identity.Features.Auth.Models.User"/>. Split out of the old global
/// <c>UserRole.Admin</c> in Phase 40.6: a РОП is the admin of one organization, not of the
/// platform.
///
/// <para>
/// Revised on 2026-08-16 (docs/DECISIONS.md): every tenancy now has both an admin and a
/// superadmin. <c>OrgAdmin</c> was renamed to <see cref="TenancyAdmin"/> at the same numeric
/// value — the column is an <c>int</c> (<c>HasConversion&lt;int&gt;()</c> in
/// <c>MembershipEntityConfiguration</c> and <c>InviteEntityConfiguration</c>), so the rename is
/// source-level only and needs no data migration and no EF migration.
/// </para>
///
/// <para>
/// <see cref="TenancyAdmin"/> and <see cref="TenancySuperAdmin"/> are identical except that only
/// a <see cref="TenancySuperAdmin"/> may add or remove users of the organization (invite, revoke
/// an invite, deactivate a membership).
/// </para>
/// </summary>
public enum OrgRole
{
    Manager = 0,

    /// <summary>Administrator of one organization. Formerly <c>OrgAdmin</c>.</summary>
    TenancyAdmin = 1,

    /// <summary>Everything <see cref="TenancyAdmin"/> can do, plus adding and removing the
    /// organization's users.</summary>
    TenancySuperAdmin = 2
}
