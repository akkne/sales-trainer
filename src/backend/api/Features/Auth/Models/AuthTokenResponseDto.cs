namespace SalesTrainer.Api.Features.Auth.Models;

public sealed record AuthTokenResponseDto(
    string AccessToken,
    string UserId,
    string DisplayName,
    bool IsOnboardingCompleted,
    string Role
);
