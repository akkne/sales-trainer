using Sellevate.Identity.Features.Auth.Exceptions;
using Sellevate.Identity.Features.Auth.Models;

namespace Sellevate.Identity.Features.Auth.Services.Abstract;

/// <summary>
/// The token surface of the platform. Every method that authenticates signals failure by throwing
/// <see cref="UnauthorizedAccessException"/> with a message that is safe to show a caller: the wording
/// is deliberately identical across "unknown address", "wrong credential" and "no access", so no
/// endpoint here can be used to enumerate accounts.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Creates an account from the public sign-up form (Phase 40.37) and signs it straight in.
    ///
    /// <para>
    /// The account carries **no** membership, and this method never creates one — joining an
    /// organization stays the invite's job. What the caller gets is therefore an identity that can
    /// log in and reach nothing but the "waiting for an invitation" screen, which is why the
    /// duplicate-address answer here can be honest (<see cref="EmailAlreadyRegisteredException"/>) while
    /// the login and Google paths keep their deliberately uninformative wording: a form that
    /// refuses to tell you the address is taken cannot let you register either.
    /// </para>
    ///
    /// <para>
    /// Whether it signs in or asks for a code is <c>EmailVerification:Enabled</c>'s decision — see
    /// <see cref="RegistrationResult"/>.
    /// </para>
    /// </summary>
    Task<RegistrationResult> RegisterWithEmailAsync(
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken = default);

    Task<IssuedTokenPair> VerifyEmailAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default);

    Task ResendVerificationCodeAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Steps 1–2 of the three-step login flow (Phase 40.8): the address is turned into the login
    /// method its organization configured, without revealing whether the address is known.
    /// </summary>
    Task<ResolvedLoginMethod> ResolveLoginMethodAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<IssuedTokenPair> LoginWithEmailAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<IssuedTokenPair> LoginWithGoogleAsync(
        string googleIdToken,
        CancellationToken cancellationToken = default);

    Task<IssuedTokenPair> RefreshAccessTokenAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default);

    Task RevokeRefreshTokenAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues the access/refresh pair for an already-authenticated user. Exposed for the invite
    /// acceptance flow (40.7), which authenticates by consuming a single-use signed token rather
    /// than by password, but must produce exactly the same claims — including <c>org_id</c> and
    /// <c>org_role</c> from the membership the acceptance just created.
    /// </summary>
    Task<IssuedTokenPair> IssueTokensForUserAsync(
        User user,
        CancellationToken cancellationToken = default);
}
