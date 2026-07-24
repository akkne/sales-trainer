using Sellevate.Identity.Features.Auth.Models;

namespace Sellevate.Identity.Features.Auth.Services.Abstract;

public interface IAuthenticationService
{
    // TEMP: email confirmation disabled — registration issues tokens immediately.
    Task<IssuedTokenPair> RegisterWithEmailAsync(
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
}
