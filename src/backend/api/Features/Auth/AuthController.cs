using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Auth.Exceptions;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.Auth.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Api.Infrastructure.Metrics;

namespace SalesTrainer.Api.Features.Auth;

[ApiController]
[Route("auth")]
public sealed class AuthController(
    IAuthenticationService authenticationService,
    AppDbContext databaseContext,
    IWebHostEnvironment environment) : ControllerBase
{
    private const string RefreshTokenCookieName = "refreshToken";
    private CookieOptions SecureHttpOnlyCookieOptions => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        MaxAge = TimeSpan.FromDays(30)
    };

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);
        var displayName = User.FindFirstValue("displayName");
        var role = User.FindFirstValue(ClaimTypes.Role);

        var isOnboardingCompleted = userId is not null && await databaseContext.UserProfiles
            .AnyAsync(profile => profile.UserId == Guid.Parse(userId) && profile.IsOnboardingCompleted, cancellationToken);

        return Ok(new
        {
            id = userId,
            email,
            displayName,
            role,
            isOnboardingCompleted
        });
    }

    [HttpPost("register")]
    public async Task<ActionResult<RegistrationResultDto>> RegisterWithEmail(
        [FromBody] RegisterRequestDto registerRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await authenticationService.RegisterWithEmailAsync(
                registerRequest.Email,
                registerRequest.Password,
                registerRequest.DisplayName,
                cancellationToken);

            return Ok(new RegistrationResultDto(
                Email: registerRequest.Email,
                RequiresEmailVerification: true));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPost("verify-email")]
    public async Task<ActionResult<AuthTokenResponseDto>> VerifyEmail(
        [FromBody] VerifyEmailRequestDto verifyEmailRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var issuedTokenPair = await authenticationService.VerifyEmailAsync(
                verifyEmailRequest.Email,
                verifyEmailRequest.Code,
                cancellationToken);

            AppMetrics.Registrations.Inc();
            return OkWithRefreshTokenCookie(issuedTokenPair);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Unauthorized(new { message = exception.Message });
        }
    }

    [HttpPost("resend-code")]
    public async Task<IActionResult> ResendVerificationCode(
        [FromBody] ResendVerificationCodeRequestDto resendRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await authenticationService.ResendVerificationCodeAsync(
                resendRequest.Email,
                cancellationToken);

            return NoContent();
        }
        catch (EmailVerificationCooldownException exception)
        {
            Response.Headers.RetryAfter = exception.RetryAfterSeconds.ToString();
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new { message = exception.Message, retryAfterSeconds = exception.RetryAfterSeconds });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthTokenResponseDto>> LoginWithEmail(
        [FromBody] LoginRequestDto loginRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var issuedTokenPair = await authenticationService.LoginWithEmailAsync(
                loginRequest.Email,
                loginRequest.Password,
                cancellationToken);

            AppMetrics.Logins.WithLabels("password").Inc();
            return OkWithRefreshTokenCookie(issuedTokenPair);
        }
        catch (EmailNotVerifiedException exception)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = exception.Message, requiresEmailVerification = true, email = exception.Email });
        }
        catch (UnauthorizedAccessException exception)
        {
            return Unauthorized(new { message = exception.Message });
        }
    }

    [HttpPost("google")]
    public async Task<ActionResult<AuthTokenResponseDto>> LoginWithGoogle(
        [FromBody] GoogleLoginRequestDto googleLoginRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var issuedTokenPair = await authenticationService.LoginWithGoogleAsync(
                googleLoginRequest.IdToken,
                cancellationToken);

            AppMetrics.Logins.WithLabels("google").Inc();
            return OkWithRefreshTokenCookie(issuedTokenPair);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or Google.Apis.Auth.InvalidJwtException)
        {
            return Unauthorized(new { message = "Invalid Google token." });
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthTokenResponseDto>> RefreshAccessToken(
        CancellationToken cancellationToken = default)
    {
        var rawRefreshToken = Request.Cookies[RefreshTokenCookieName];

        if (string.IsNullOrEmpty(rawRefreshToken))
            return Unauthorized(new { message = "Refresh token missing." });

        try
        {
            var issuedTokenPair = await authenticationService.RefreshAccessTokenAsync(
                rawRefreshToken,
                cancellationToken);
            return OkWithRefreshTokenCookie(issuedTokenPair);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Unauthorized(new { message = exception.Message });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken = default)
    {
        var rawRefreshToken = Request.Cookies[RefreshTokenCookieName];

        if (!string.IsNullOrEmpty(rawRefreshToken))
            await authenticationService.RevokeRefreshTokenAsync(rawRefreshToken, cancellationToken);

        Response.Cookies.Delete(RefreshTokenCookieName);
        return NoContent();
    }

    private OkObjectResult OkWithRefreshTokenCookie(IssuedTokenPair issuedTokenPair)
    {
        Response.Cookies.Append(
            RefreshTokenCookieName,
            issuedTokenPair.RefreshToken,
            SecureHttpOnlyCookieOptions);

        return Ok(new AuthTokenResponseDto(
            AccessToken: issuedTokenPair.AccessToken,
            UserId: issuedTokenPair.UserId,
            DisplayName: issuedTokenPair.DisplayName,
            IsOnboardingCompleted: issuedTokenPair.IsOnboardingCompleted,
            Role: issuedTokenPair.Role.ToString()));
    }
}
