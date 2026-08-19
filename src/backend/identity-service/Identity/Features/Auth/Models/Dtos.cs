using System.ComponentModel.DataAnnotations;

namespace Sellevate.Identity.Features.Auth.Models;

/// <summary>
/// Self-service sign-up (Phase 40.37). It asks for no organization because it creates none: the
/// account this produces holds no membership until someone invites it.
/// </summary>
public sealed record RegisterRequestDto(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8), MaxLength(128)] string Password,
    [Required, MaxLength(100)] string DisplayName);

/// <summary>
/// Sign-up ends one of two ways depending on <c>EmailVerification:Enabled</c>, so the result carries
/// which one happened rather than leaving the controller to re-read the flag and guess.
/// <see cref="TokenPair"/> is null exactly when <see cref="RequiresEmailVerification"/> is true.
/// </summary>
public sealed record RegistrationResult(
    IssuedTokenPair? TokenPair,
    bool RequiresEmailVerification,
    string Email);

/// <summary>
/// The body of the 202 that answers a registration still owing a code — deliberately the same
/// <c>requiresEmailVerification</c> / <c>email</c> pair the 403 from <c>POST /auth/login</c> uses, so
/// the client has one branch to write, not two.
/// </summary>
public sealed record RegistrationPendingVerificationDto(
    string Email,
    bool RequiresEmailVerification);

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
