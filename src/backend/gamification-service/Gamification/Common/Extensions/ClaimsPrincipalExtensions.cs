using System.Security.Claims;

namespace Sellevate.Gamification.Common.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static bool TryResolveUserId(this ClaimsPrincipal principal, out Guid userId)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var rawUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        return Guid.TryParse(rawUserId, out userId);
    }
}
