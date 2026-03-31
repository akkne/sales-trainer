namespace SalesTrainer.Api.Features.Auth;

public record AuthTokenResponseDto(
    string AccessToken,
    string UserId,
    string DisplayName,
    bool IsOnboardingCompleted
);
