using Microsoft.AspNetCore.Http;
using Sellevate.BuildingBlocks.Identity;

namespace Sellevate.Analytics.Common;

public static class CurrentUserAccessor
{
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
