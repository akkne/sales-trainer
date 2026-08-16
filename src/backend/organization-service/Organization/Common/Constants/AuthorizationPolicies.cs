using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Sellevate.Organization.Common.Constants;

/// <summary>
/// The four authorization policies of the platform, declared identically in every service so a
/// route means the same thing wherever it lives. Revised on 2026-08-16 by the owner's role split
/// (docs/DECISIONS.md): `Admin` and `SuperAdmin` are Sellevate's own platform roles and are not
/// bounded by tenancy, while every organization additionally has its own `TenancyAdmin` and
/// `TenancySuperAdmin`.
///
/// <para>
/// The only privilege that separates an admin from a superadmin — at either level — is adding and
/// removing users. That is what <see cref="RequireOrganizationSuperAdministrator"/> and
/// <see cref="RequireSuperAdministrator"/> exist to gate; everything else an admin can reach.
/// </para>
///
/// <para>
/// Platform staff satisfy the organization-scoped policies without holding any `org_role` claim
/// at all, which is deliberate: a Sellevate administrator normally has no membership anywhere and
/// must still be able to work. Making the data they then see span every organization is a
/// separate concern living in the tenancy layer, not here.
/// </para>
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Platform `Admin` or `SuperAdmin`. The gate for platform content administration.</summary>
    public const string RequirePlatformAdministrator = "RequirePlatformAdmin";

    /// <summary>Platform `SuperAdmin` only. Reserved for adding/removing users at platform level
    /// and for impersonation.</summary>
    public const string RequireSuperAdministrator = "RequireSuperAdmin";

    /// <summary>An organization's own administrator, or any platform administrator.</summary>
    public const string RequireOrganizationAdministrator = "RequireOrgAdmin";

    /// <summary>An organization's own superadmin, or a platform `SuperAdmin`. Gates adding and
    /// removing the organization's users — and nothing else.</summary>
    public const string RequireOrganizationSuperAdministrator = "RequireOrgSuperAdmin";

    /// <summary>Value of the JWT `role` claim for a platform administrator.</summary>
    public const string AdministratorRole = "Admin";

    /// <summary>Value of the JWT `role` claim for a platform superadministrator.</summary>
    public const string SuperAdministratorRole = "SuperAdmin";

    /// <summary>Value of the JWT `org_role` claim for an organization's administrator.</summary>
    public const string TenancyAdministratorOrganizationRole = "TenancyAdmin";

    /// <summary>Value of the JWT `org_role` claim for an organization's superadministrator.</summary>
    public const string TenancySuperAdministratorOrganizationRole = "TenancySuperAdmin";

    /// <summary>The JWT claim carrying the organization-scoped role, minted by identity-service.</summary>
    public const string OrganizationRoleClaimType = "org_role";

    /// <summary>
    /// Registers all four policies. Call as `services.AddAuthorization(AuthorizationPolicies.Register)`
    /// so no service can drift into its own interpretation of a policy name.
    /// </summary>
    public static void Register(AuthorizationOptions authorizationOptions)
    {
        authorizationOptions.AddPolicy(RequirePlatformAdministrator, policy =>
            policy.RequireAssertion(authorizationContext =>
                IsPlatformAdministrator(authorizationContext.User)));

        authorizationOptions.AddPolicy(RequireSuperAdministrator, policy =>
            policy.RequireRole(SuperAdministratorRole));

        authorizationOptions.AddPolicy(RequireOrganizationAdministrator, policy =>
            policy.RequireAssertion(authorizationContext =>
                IsPlatformAdministrator(authorizationContext.User)
                || HasOrganizationRole(authorizationContext.User, TenancyAdministratorOrganizationRole)
                || HasOrganizationRole(authorizationContext.User, TenancySuperAdministratorOrganizationRole)));

        authorizationOptions.AddPolicy(RequireOrganizationSuperAdministrator, policy =>
            policy.RequireAssertion(authorizationContext =>
                authorizationContext.User.IsInRole(SuperAdministratorRole)
                || HasOrganizationRole(
                    authorizationContext.User, TenancySuperAdministratorOrganizationRole)));
    }

    private static bool IsPlatformAdministrator(ClaimsPrincipal authenticatedPrincipal)
        => authenticatedPrincipal.IsInRole(AdministratorRole)
           || authenticatedPrincipal.IsInRole(SuperAdministratorRole);

    private static bool HasOrganizationRole(
        ClaimsPrincipal authenticatedPrincipal, string organizationRole)
        => authenticatedPrincipal.HasClaim(claim =>
            claim.Type == OrganizationRoleClaimType && claim.Value == organizationRole);
}
