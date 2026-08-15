using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Identity.Features.Auth.Exceptions;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Auth.Services.Abstract;
using Sellevate.Identity.Features.Invites.Exceptions;
using Sellevate.Identity.Features.Invites.Models;
using Sellevate.Identity.Features.Invites.Services.Abstract;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.Auth;

[ApiController]
[Route("auth")]
public sealed class AuthController(
    IAuthenticationService authenticationService,
    IInviteService inviteService,
    IdentityDbContext databaseContext,
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
        var orgId = User.FindFirstValue("org_id");
        var orgRole = User.FindFirstValue("org_role");

        var isOnboardingCompleted = Guid.TryParse(userId, out var parsedUserId) && await databaseContext.UserProfiles
            .AnyAsync(profile => profile.UserId == parsedUserId && profile.IsOnboardingCompleted, cancellationToken);

        return Ok(new
        {
            id = userId,
            email,
            displayName,
            role,
            orgId,
            orgRole,
            isOnboardingCompleted
        });
    }

    /// <summary>
    /// Consumes a single-use invite token and signs the invitee in. This is the only way into the
    /// product now that <c>POST /auth/register</c> is gone (docs/TENANCY/TENANCY.md §4.1).
    ///
    /// <para>
    /// Anonymous and deliberately not <c>[TenantScoped]</c>: the caller has no organization yet, so
    /// there is no <c>X-Organization-Id</c> header to scope by. The organization is recovered from
    /// the token's HMAC-signed payload instead — see
    /// <c>Invites.Services.Implementation.InviteTokenFactory</c>.
    /// </para>
    /// </summary>
    [HttpPost("invites/{token}/accept")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthTokenResponseDto>> AcceptInvite(
        string token,
        [FromBody] AcceptInviteRequestDto acceptInviteRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var issuedTokenPair = await inviteService.AcceptAsync(token, acceptInviteRequest, cancellationToken);
            return OkWithRefreshTokenCookie(issuedTokenPair);
        }
        catch (InviteNotAcceptableException exception)
        {
            return exception.Reason switch
            {
                InviteRejectionReason.NotFound => NotFound(new { message = exception.Message }),
                InviteRejectionReason.AlreadyAccepted => Conflict(new { message = exception.Message }),
                InviteRejectionReason.PasswordRequired => BadRequest(new { message = exception.Message }),
                _ => StatusCode(StatusCodes.Status410Gone, new { message = exception.Message })
            };
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

            return OkWithRefreshTokenCookie(issuedTokenPair);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Unauthorized(new { message = exception.Message });
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
        {
            return Unauthorized(new { message = "Refresh token missing." });
        }

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
        {
            await authenticationService.RevokeRefreshTokenAsync(rawRefreshToken, cancellationToken);
        }

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
            Role: issuedTokenPair.Role.ToString(),
            OrgId: issuedTokenPair.OrgId,
            OrgRole: issuedTokenPair.OrgRole));
    }
}
