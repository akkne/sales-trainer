namespace Sellevate.Identity.Features.Auth.Models;

public sealed record RegisterRequestDto(string Email, string Password, string DisplayName);

public sealed record LoginRequestDto(string Email, string Password);

public sealed record VerifyEmailRequestDto(string Email, string Code);

public sealed record GoogleLoginRequestDto(string IdToken);

public sealed record ResendVerificationCodeRequestDto(string Email);

public sealed record RegistrationResultDto(string Email, bool RequiresEmailVerification);

public sealed record AuthTokenResponseDto(
    string AccessToken,
    string UserId,
    string DisplayName,
    bool IsOnboardingCompleted,
    string Role
);

public sealed record IssuedTokenPair(
    string AccessToken,
    string RefreshToken,
    string UserId,
    string DisplayName,
    bool IsOnboardingCompleted,
    UserRole Role
);
