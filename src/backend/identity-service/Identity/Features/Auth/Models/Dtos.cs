using System.ComponentModel.DataAnnotations;

namespace Sellevate.Identity.Features.Auth.Models;

public sealed record RegisterRequestDto(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8), MaxLength(128)] string Password,
    [Required, MaxLength(100)] string DisplayName);

public sealed record LoginRequestDto(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public sealed record VerifyEmailRequestDto(
    [Required, EmailAddress] string Email,
    [Required] string Code);

public sealed record GoogleLoginRequestDto([Required] string IdToken);

public sealed record ResendVerificationCodeRequestDto([Required, EmailAddress] string Email);

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
