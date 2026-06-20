using System.ComponentModel.DataAnnotations;

namespace Sellevate.Identity.Features.Auth.Models;

public sealed record RegisterRequestDto(
    [property: Required, EmailAddress] string Email,
    [property: Required, MinLength(8), MaxLength(128)] string Password,
    [property: Required, MaxLength(100)] string DisplayName);

public sealed record LoginRequestDto(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);

public sealed record VerifyEmailRequestDto(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Code);

public sealed record GoogleLoginRequestDto([property: Required] string IdToken);

public sealed record ResendVerificationCodeRequestDto([property: Required, EmailAddress] string Email);

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
