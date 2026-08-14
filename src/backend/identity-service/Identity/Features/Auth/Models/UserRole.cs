namespace Sellevate.Identity.Features.Auth.Models;

// Phase 40.6: `Admin` (global platform admin) is removed. A РОП/manager's admin
// rights are scoped to one organization now (see Sellevate.Identity.Features.Membership.Models.OrgRole),
// never to the platform. `SuperAdmin` remains the only platform-wide role — Sellevate
// staff who can create organizations and act across tenants. Value 1 (formerly `Admin`)
// is intentionally left unassigned rather than reused, so any pre-existing `Role = 1`
// row fails to deserialize loudly instead of silently becoming something else; migrating
// such rows is 40.9's job (see docs/DECISIONS.md, docs/DONT_FORGET.md).
public enum UserRole
{
    User = 0,
    SuperAdmin = 2
}
