using System.Security.Claims;

namespace Sellevate.BuildingBlocks.Tenancy;

/// <summary>
/// The single source for the two `role` claim values that identify Sellevate's own staff, and for the
/// question "is this caller platform staff".
///
/// <para>
/// These two strings decide who reads across every organization: <see cref="IsPlatformStaff"/> is what
/// <see cref="TenantContextMiddleware"/> uses to put a request into platform-wide mode, which in turn
/// widens every EF global query filter and sets the <c>app.platform_mode</c> GUC the row-level security
/// policies read. Before this class existed the same two literals were written independently in the
/// middleware and in six services' <c>AuthorizationPolicies</c>, and the "Admin or SuperAdmin"
/// predicate was hand-rolled at three more call sites. All of them agreed, but nothing made them
/// agree — a rename in one place would have silently widened or narrowed tenant visibility at a
/// security boundary, in whichever direction the surviving copy pointed.
/// </para>
///
/// <para>
/// These are platform roles from the `role` claim, <b>never</b> an organization's own `org_role`. The
/// organization-scoped vocabulary (<c>TenancyAdmin</c>, <c>TenancySuperAdmin</c>) is a separate
/// concept introduced by the 2026-08-16 role split; see <c>docs/DECISIONS.md</c>. Each service's
/// <c>AuthorizationPolicies</c> still declares its own policy <i>names</i>, because those are
/// per-service registrations, and aliases the two role <i>values</i> to here.
/// </para>
/// </summary>
public static class PlatformRoles
{
    /// <summary>Value of the JWT `role` claim for a platform administrator.</summary>
    public const string Administrator = "Admin";

    /// <summary>Value of the JWT `role` claim for a platform superadministrator.</summary>
    public const string SuperAdministrator = "SuperAdmin";

    /// <summary>
    /// Both platform roles. Order is not significant; membership is.
    /// </summary>
    public static readonly IReadOnlyList<string> All = [Administrator, SuperAdministrator];

    /// <summary>
    /// <see langword="true"/> when <paramref name="principal"/> holds either platform role.
    ///
    /// <para>
    /// Returns <see langword="false"/> for an unauthenticated principal, so a request that never
    /// carried a token cannot reach platform-wide mode. Callers deciding what a platform
    /// administrator may <i>do</i> should use an authorization policy instead; this answers only
    /// what they may <i>see across organizations</i>.
    /// </para>
    /// </summary>
    public static bool IsPlatformStaff(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return All.Any(principal.IsInRole);
    }
}
