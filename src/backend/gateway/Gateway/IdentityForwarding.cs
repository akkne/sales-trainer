using System.Net.Http.Headers;
using System.Security.Claims;
using Sellevate.BuildingBlocks.Identity;

namespace Sellevate.Gateway;

/// <summary>
/// Builds the trusted identity headers forwarded to downstream services. The gateway
/// validates the JWT once; downstream services then read <see cref="IdentityHeaders.UserId"/> /
/// <see cref="IdentityHeaders.UserRole"/> instead of re-validating the token.
///
/// <para>
/// Security-critical: any client-supplied copies of these headers are always stripped
/// first, then re-added <em>only</em> from the validated principal — so a caller can
/// never spoof their identity by setting the header themselves.
/// </para>
/// </summary>
internal static class IdentityForwarding
{
    /// <summary>
    /// Rewrites <paramref name="proxyHeaders"/> in place: removes any inbound identity
    /// headers, then sets them from <paramref name="user"/> when authenticated.
    /// </summary>
    public static void Apply(HttpRequestHeaders proxyHeaders, ClaimsPrincipal user)
    {
        proxyHeaders.Remove(IdentityHeaders.UserId);
        proxyHeaders.Remove(IdentityHeaders.UserRole);

        if (user.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userId = IdentityHeaders.ResolveUserId(user);
        if (!string.IsNullOrEmpty(userId))
        {
            proxyHeaders.Add(IdentityHeaders.UserId, userId);
        }

        var role = IdentityHeaders.ResolveRole(user);
        if (!string.IsNullOrEmpty(role))
        {
            proxyHeaders.Add(IdentityHeaders.UserRole, role);
        }
    }
}
