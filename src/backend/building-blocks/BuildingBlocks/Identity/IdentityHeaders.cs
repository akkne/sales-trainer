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

    /// <summary>
    /// Extracts the user id from a validated principal. Falls back across the common
    /// claim types JWTs use for the subject (<c>sub</c> / NameIdentifier).
    /// </summary>
    public static string? ResolveUserId(ClaimsPrincipal principal)
        => principal.FindFirst("sub")?.Value
           ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    /// <summary>Extracts the role from a validated principal (<c>role</c> / Role claim).</summary>
    public static string? ResolveRole(ClaimsPrincipal principal)
        => principal.FindFirst(ClaimTypes.Role)?.Value
           ?? principal.FindFirst("role")?.Value;
}
