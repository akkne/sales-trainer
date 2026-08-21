using System.Security.Claims;

namespace Sellevate.BuildingBlocks.Tenancy;

/// <summary>
/// The single source for the `isDemo` claim minted by <c>DemoTokenController</c>, and for the
/// question "is this caller the throwaway demo identity". Mirrors <see cref="PlatformRoles"/>: one
/// predicate, referenced from both the issuer and <see cref="TenantContextMiddleware"/>, so the two
/// cannot silently disagree on the claim's spelling.
///
/// <para>
/// A demo caller has no organization membership and is not platform staff, so without this
/// exemption <see cref="TenantScopedAttribute"/> would 403 every learner-facing route for it —
/// see docs/AUDIT_NIGHT_REVIEW.md R-18 and the invariant recorded in
/// <c>awaiting-organization-gate.tsx</c> ("the demo token — which has no user row at all — keeps
/// working"). The exemption widens nothing: it only lets the request past the gate. It does
/// <b>not</b> enter platform-wide mode, so <see cref="ITenantContext.OrganizationId"/> stays
/// <see langword="null"/> and <see cref="ITenantContext.IsPlatformWide"/> stays
/// <see langword="false"/> — the same "neither organization nor platform-wide" state
/// <see cref="TenantConnectionInterceptor"/> already treats as fail-closed (RLS GUCs unset, EF
/// query filters resolve to global-content-only or empty). A demo caller therefore sees the
/// global curriculum and nothing tenant-scoped, never another organization's data.
/// </para>
/// </summary>
public static class DemoCallers
{
    /// <summary>The JWT claim type <c>DemoTokenController</c> mints on a demo token.</summary>
    public const string IsDemoClaimType = "isDemo";

    /// <summary>
    /// <see langword="true"/> when <paramref name="principal"/> is authenticated and carries the
    /// demo claim with value <c>"true"</c>. Returns <see langword="false"/> for an unauthenticated
    /// principal, matching <see cref="PlatformRoles.IsPlatformStaff"/>.
    /// </summary>
    public static bool IsDemoCaller(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return principal.HasClaim(IsDemoClaimType, bool.TrueString.ToLowerInvariant());
    }
}
