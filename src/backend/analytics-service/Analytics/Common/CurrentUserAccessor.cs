using Microsoft.AspNetCore.Http;
using Sellevate.BuildingBlocks.Identity;

namespace Sellevate.Analytics.Common;

/// <summary>
/// Resolves the caller's identity from a request, preferring the gateway-injected
/// <c>X-User-Id</c> header over the JWT subject.
///
/// <para>
/// The order is load-bearing: the header is set by the gateway only after it has validated the
/// token, and it is what keeps the identity the same value every service behind the gateway sees.
/// The JWT fallback exists for a direct call — an integration test, or a service reached without the
/// gateway in front of it — where the header cannot be there.
/// </para>
/// </summary>
public static class CurrentUserAccessor
{
    /// <summary>
    /// Returns <c>null</c> when neither source carries an identity, which the caller must treat as
    /// unauthenticated rather than as an anonymous user.
    /// </summary>
    public static string? ResolveUserId(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var gatewayUserId = httpContext.Request.Headers[IdentityHeaders.UserId].ToString();
        if (!string.IsNullOrWhiteSpace(gatewayUserId))
        {
            return gatewayUserId;
        }

        return IdentityHeaders.ResolveUserId(httpContext.User);
    }
}
