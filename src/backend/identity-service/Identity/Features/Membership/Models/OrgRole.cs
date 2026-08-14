namespace Sellevate.Identity.Features.Membership.Models;

/// <summary>
/// The organization-scoped role, held on <see cref="Membership"/> — never on
/// <see cref="Sellevate.Identity.Features.Auth.Models.User"/>. Split out of the old global
/// <c>UserRole.Admin</c> in Phase 40.6: a РОП is the admin of one organization, not of the
/// platform.
/// </summary>
public enum OrgRole
{
    Manager = 0,
    OrgAdmin = 1
}
