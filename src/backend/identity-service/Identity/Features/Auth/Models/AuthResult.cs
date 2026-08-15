namespace Sellevate.Identity.Features.Auth.Models;

/// <summary>
/// The outcome of one <c>IAuthProvider.AuthenticateAsync</c> call: the authenticated user, or
/// nothing. A provider never issues tokens itself — that stays in
/// <c>AuthenticationService.IssueTokensForUserAsync</c>, so every login method produces the same
/// session, claims and refresh-token family no matter which provider proved the identity.
///
/// <para>
/// There is deliberately no failure reason. The caller turns every failure into one identical
/// <c>401</c>, the same choice 40.7 made for Google sign-in: a distinguishable answer would tell
/// an outsider which addresses belong to a Sellevate customer.
/// </para>
/// </summary>
public sealed record AuthResult(User? AuthenticatedUser)
{
    public static AuthResult Failed { get; } = new((User?)null);

    public static AuthResult Succeeded(User authenticatedUser) => new(authenticatedUser);

    public bool IsAuthenticated => AuthenticatedUser is not null;
}
