namespace SalesTrainer.Api.Features.Auth.Models;

public sealed record IssuedTokenPair(
    string AccessToken,
    string RefreshToken,
    string UserId,
    string DisplayName,
    bool IsOnboardingCompleted,
    UserRole Role
);
