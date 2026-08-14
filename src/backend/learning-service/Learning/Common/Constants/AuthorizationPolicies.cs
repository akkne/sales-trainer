namespace Sellevate.Learning.Common.Constants;

// Phase 40.6: the global `Admin` role (AdministratorRole) is gone. `RequireOrgAdmin` is
// new infrastructure for the organization-scoped role (`org_role` claim on the JWT); no
// call site in this service yet — ready for the org admin screen (40.20).
public static class AuthorizationPolicies
{
    public const string RequireOrgAdmin = "RequireOrgAdmin";
    public const string RequireSuperAdministrator = "RequireSuperAdmin";
    public const string OrgAdminOrgRole = "OrgAdmin";
    public const string SuperAdministratorRole = "SuperAdmin";
}
