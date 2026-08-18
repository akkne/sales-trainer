using System.Security.Claims;

namespace Sellevate.Social.Common.Extensions;

/// <summary>
/// Resolves the caller's user id from the bearer token, in the one place every controller in this
/// service now shares. Two claim types are consulted in order because the platform's own tokens
/// carry <see cref="ClaimTypes.NameIdentifier"/> while third-party issuers put the subject in
/// <c>sub</c>; a controller that consulted only one of them would refuse half the valid tokens.
/// An unparsable or absent claim is "no user", never a zero user id, so a caller must branch on the
/// returned flag rather than on the value.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    private const string SubjectClaimType = "sub";

    /// <summary>
    /// <see langword="true"/> and the caller's id when the token carries a parsable user id;
    /// otherwise <see langword="false"/>, with <paramref name="userId"/> left at
    /// <see cref="Guid.Empty"/>.
    /// </summary>
    public static bool TryResolveUserId(this ClaimsPrincipal principal, out Guid userId)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var rawUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(SubjectClaimType);

        return Guid.TryParse(rawUserId, out userId);
    }

    /// <summary>
    /// The nullable form of <see cref="TryResolveUserId"/>, for call sites that pass the result on
    /// as an optional value instead of branching immediately.
    /// </summary>
    public static Guid? ResolveUserIdOrNull(this ClaimsPrincipal principal)
        => principal.TryResolveUserId(out var userId) ? userId : null;
}
