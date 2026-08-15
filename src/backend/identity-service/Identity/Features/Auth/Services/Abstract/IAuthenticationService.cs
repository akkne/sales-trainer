using Sellevate.Identity.Features.Auth.Models;

namespace Sellevate.Identity.Features.Auth.Services.Abstract;

public interface IAuthenticationService
{
    Task<IssuedTokenPair> VerifyEmailAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default);

    Task ResendVerificationCodeAsync(
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
