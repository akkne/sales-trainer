namespace SalesTrainer.Api.Features.Auth;

public interface IAuthenticationService
{
    Task<IssuedTokenPair> RegisterWithEmailAsync(
        string email,
        string password,
        string displayName,
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
