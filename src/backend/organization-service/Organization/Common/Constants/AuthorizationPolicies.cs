namespace Sellevate.Organization.Common.Constants;

/// <summary>
/// Mirrors the policy names used by the other services (see learning-service and
/// gamification-service's identically named files). Phase 40.9 gives organization-service its
/// first policy: the tenant registry is administered by Sellevate staff only.
/// </summary>
public static class AuthorizationPolicies
{
    public const string RequireSuperAdmin = "RequireSuperAdmin";

    /// <summary>The platform role carried in the JWT <c>role</c> claim, minted by
    /// identity-service. Organization roles (<c>org_role</c>) are a different axis and never grant
    /// access here.</summary>
    public const string SuperAdminRoleName = "SuperAdmin";
}
