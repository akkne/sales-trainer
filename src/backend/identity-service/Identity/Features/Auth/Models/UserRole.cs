namespace Sellevate.Identity.Features.Auth.Models;

/// <summary>
/// The platform-wide role, held on <see cref="User"/> and carried in the JWT <c>role</c> claim.
/// These are Sellevate's own staff roles: they are deliberately NOT limited by tenancy and are
/// meant to see across every organization. An organization's own administrators live on a
/// different axis entirely — see
/// <see cref="Sellevate.Identity.Features.Membership.Models.OrgRole"/>.
///
/// <para>
/// Phase 40.6 removed <c>Admin</c> and left value 1 unassigned so that legacy <c>Role = 1</c> rows
/// would fail loudly. The owner reinstated it on 2026-08-16 (see docs/DECISIONS.md), and reusing
/// value 1 is the correct move rather than a dangerous one: 1 meant exactly "global platform
/// admin" before 40.6, so any surviving legacy row lands back on precisely the meaning it already
/// had — there is nothing to migrate and nothing to reinterpret.
/// </para>
///
/// <para>
/// <see cref="Admin"/> and <see cref="SuperAdmin"/> are identical in every respect except one:
/// only a <see cref="SuperAdmin"/> may add or remove users (create, invite, deactivate, or change
/// the role of a user or membership). Everything else — all platform content administration — is
/// open to both.
/// </para>
/// </summary>
public enum UserRole
{
    User = 0,

    /// <summary>Sellevate staff. Platform-wide, tenancy-unbounded, read/write on content —
    /// but may not add or remove users.</summary>
    Admin = 1,

    /// <summary>Sellevate staff. Everything <see cref="Admin"/> can do, plus adding and
    /// removing users.</summary>
    SuperAdmin = 2
}
