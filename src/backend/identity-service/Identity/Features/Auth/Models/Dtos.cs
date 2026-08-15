using System.ComponentModel.DataAnnotations;

namespace Sellevate.Identity.Features.Auth.Models;

public sealed record LoginRequestDto(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public sealed record VerifyEmailRequestDto(
    [Required, EmailAddress] string Email,
    [Required] string Code);

public sealed record GoogleLoginRequestDto([Required] string IdToken);

public sealed record ResendVerificationCodeRequestDto([Required, EmailAddress] string Email);

public sealed record AuthTokenResponseDto(
    string AccessToken,
    string UserId,
    string DisplayName,
    bool IsOnboardingCompleted,
    string Role,
    string? OrgId,
    string? OrgRole
);

public sealed record IssuedTokenPair(
    string AccessToken,
    string RefreshToken,
    string UserId,
    string DisplayName,
    bool IsOnboardingCompleted,
    UserRole Role,
    string? OrgId,
    string? OrgRole
);
