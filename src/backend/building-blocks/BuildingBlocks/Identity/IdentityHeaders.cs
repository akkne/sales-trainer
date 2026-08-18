using System.Security.Claims;

namespace Sellevate.BuildingBlocks.Identity;

/// <summary>
/// The trusted identity headers the API Gateway injects into every downstream request
/// after it validates the JWT once. Downstream services read these instead of
/// re-validating the token, so identity flows in one well-known shape.
///
/// <para>
/// Security rule: the gateway must <em>strip</em> any client-supplied copies of these
/// headers and set them solely from the validated token — a service trusts them only
/// because they arrive through the gateway.
/// </para>
/// </summary>
public static class IdentityHeaders
{
    public const string UserId = "X-User-Id";
    public const string UserRole = "X-User-Role";
    public const string OrganizationId = "X-Organization-Id";

    private const string SubjectClaimType = "sub";
    private const string RoleClaimType = "role";
    private const string OrganizationIdClaimType = "org_id";

    /// <summary>
    /// Extracts the user id from a validated principal. Falls back across the common
    /// claim types JWTs use for the subject (<c>sub</c> / NameIdentifier).
    /// </summary>
    public static string? ResolveUserId(ClaimsPrincipal principal)
        => principal.FindFirst(SubjectClaimType)?.Value
           ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    /// <summary>Extracts the role from a validated principal (<c>role</c> / Role claim).</summary>
    public static string? ResolveRole(ClaimsPrincipal principal)
        => principal.FindFirst(ClaimTypes.Role)?.Value
           ?? principal.FindFirst(RoleClaimType)?.Value;

    /// <summary>
    /// Extracts the tenant from the <c>org_id</c> claim of a validated principal, returning
    /// <see langword="null"/> for a principal with no organization membership (platform staff, or a
    /// token minted before the claim existed). Read only from the claim — never from the
    /// <see cref="OrganizationId"/> header on an inbound request, which the gateway strips precisely
    /// so that a client cannot name its own tenant.
    /// </summary>
    public static string? ResolveOrganizationId(ClaimsPrincipal principal)
        => principal.FindFirst(OrganizationIdClaimType)?.Value;
}
