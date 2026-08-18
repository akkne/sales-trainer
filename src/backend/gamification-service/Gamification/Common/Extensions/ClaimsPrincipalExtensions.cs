using System.Security.Claims;
using Sellevate.Gamification.Common.Constants;

namespace Sellevate.Gamification.Common.Extensions;

/// <summary>
/// Reads the caller's identity off an authenticated principal.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// True when the principal carries a parseable user id. Returns false rather than throwing or
    /// yielding <c>Guid.Empty</c>, so a controller has to decide what an unidentifiable caller means
    /// instead of silently querying somebody else's rows.
    /// </summary>
    public static bool TryResolveUserId(this ClaimsPrincipal principal, out Guid userId)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var rawUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(ClaimTypeNames.Subject);

        return Guid.TryParse(rawUserId, out userId);
    }
}
