using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Identity.Common.Constants;
using Sellevate.Identity.Features.Auth.Exceptions;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Auth.Services.Abstract;
using Sellevate.Identity.Features.Invites.Exceptions;
using Sellevate.Identity.Features.Invites.Models;
using Sellevate.Identity.Features.Invites.Services.Abstract;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.Auth;

/// <summary>
/// The public authentication surface: invite acceptance, email verification, the three-step login flow,
/// Google sign-in, refresh and logout.
///
/// <para>
/// The refresh token never appears in a response body. It is set as an HttpOnly, SameSite=Strict cookie
/// so page script cannot read it, and <c>Secure</c> is switched off only in Development, where there is
/// no https to carry it.
/// </para>
/// </summary>
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
        var displayName = User.FindFirstValue(ClaimTypeNames.DisplayName);
        var role = User.FindFirstValue(ClaimTypes.Role);
        var organizationId = User.FindFirstValue(ClaimTypeNames.OrganizationId);
        var organizationRole = User.FindFirstValue(AuthorizationPolicies.OrganizationRoleClaimType);

        var isOnboardingCompleted = Guid.TryParse(userId, out var parsedUserId) && await databaseContext.UserProfiles
            .AnyAsync(profile => profile.UserId == parsedUserId && profile.IsOnboardingCompleted, cancellationToken);

        return Ok(new
        {
            id = userId,
            email,
            displayName,
            role,
            orgId = organizationId,
            orgName = await ResolveOrganizationNameAsync(organizationId, cancellationToken),
            orgRole = organizationRole,
            isOnboardingCompleted
        });
    }

    /// <summary>
    /// The display name of the caller's own organization, or <see langword="null"/> when they belong
    /// to none.
    ///
    /// <para>
    /// Added in Phase 40.20: the organization admin panel has to say whose panel it is, and until
    /// now nothing told a member the name of their own organization — the claim carries only the id,
    /// and <c>GET /organizations/{id}</c> is platform-staff only. Putting it in the token instead
    /// would have meant a rename only taking effect after everyone signs in again.
    /// </para>
    ///
    /// <para>
    /// Reads the local registry projection rather than calling organization-service, so this stays
    /// off the authentication hot path and survives that service being down. A name of
    /// <see langword="null"/> for a real organization means the projection has not consumed
    /// <c>organization.created</c> yet — the panel falls back to a neutral label rather than
    /// blocking on it.
    /// </para>
    /// </summary>
    private async Task<string?> ResolveOrganizationNameAsync(
        string? organizationId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(organizationId, out var parsedOrganizationId))
        {
            return null;
        }

        return await databaseContext.OrganizationReplicas
            .Where(replica => replica.OrganizationId == parsedOrganizationId)
            .Select(replica => replica.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Public sign-up (Phase 40.37, docs/TENANCY/TENANCY.md §4.1a). Creates an account with no
    /// membership and signs it in; the client then shows the "waiting for an invitation" screen.
    ///
    /// <para>
    /// Anonymous, and deliberately not <c>[TenantScoped]</c> for the same reason invite acceptance
    /// is not: the caller holds no token and no <c>X-Organization-Id</c> header, and here there is
    /// no organization to scope to at all — that is the point of the route.
    /// </para>
    ///
    /// <para>
    /// Two success shapes, decided by <c>EmailVerification:Enabled</c>: <c>200</c> with a session
    /// when the address needs no proving, and <c>202</c> naming the address when a code has just
    /// been mailed and no session exists yet.
    /// </para>
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthTokenResponseDto>> RegisterWithEmail(
        [FromBody] RegisterRequestDto registerRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var registrationResult = await authenticationService.RegisterWithEmailAsync(
                registerRequest.Email,
                registerRequest.Password,
                registerRequest.DisplayName,
                cancellationToken);

            if (registrationResult.TokenPair is not { } issuedTokenPair)
            {
                return Accepted(new RegistrationPendingVerificationDto(
                    registrationResult.Email, RequiresEmailVerification: true));
            }

            return OkWithRefreshTokenCookie(issuedTokenPair);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    /// <summary>
    /// Consumes a single-use invite token and signs the invitee in. Since 40.37 it is no longer the
    /// only way into the product, but it remains the only way into an <em>organization</em>: sign-up
    /// creates an identity, an invite is what attaches it to a company
    /// (docs/TENANCY/TENANCY.md §4.1a).
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

    /// <summary>
    /// Read-only pre-check for the acceptance page (docs/AUDIT_PROD.md X-10): lets the frontend tell
    /// a garbage, expired, revoked, or already-used token apart from a live one <em>before</em> it
    /// asks the invitee to fill in a name and password, instead of only finding out after that trip
    /// through the form. Anonymous and not <c>[TenantScoped]</c> for the same reason acceptance is
    /// not — see <see cref="AcceptInvite"/>.
    /// </summary>
    [HttpGet("invites/{token}/status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetInviteStatus(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            await inviteService.ValidateAsync(token, cancellationToken);
            return NoContent();
        }
        catch (InviteNotAcceptableException exception)
        {
            return exception.Reason switch
            {
                InviteRejectionReason.NotFound => NotFound(new { message = exception.Message }),
                InviteRejectionReason.AlreadyAccepted => Conflict(new { message = exception.Message }),
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

    /// <summary>
    /// Step 1 of the three-step login flow (Phase 40.8, docs/TENANCY/TENANCY.md §4.5): the client
    /// sends only the address and is told which credential to ask for next.
    ///
    /// <para>
    /// Pre-authentication and therefore **not** <c>[TenantScoped]</c>: the caller has no token and
    /// no <c>X-Organization-Id</c> header yet, which is exactly what this step exists to resolve.
    /// </para>
    ///
    /// <para>
    /// It answers <c>200</c> for every syntactically valid address, known or not, and never names
    /// the organization — otherwise the endpoint would be a free account-enumeration oracle. Same
    /// choice 40.7 made for <c>POST /auth/google</c>'s single identical <c>401</c>.
    /// </para>
    /// </summary>
    [HttpPost("login/start")]
    public async Task<ActionResult<LoginStartResponseDto>> StartLogin(
        [FromBody] LoginStartRequestDto loginStartRequest,
        CancellationToken cancellationToken = default)
    {
        var resolvedLoginMethod = await authenticationService.ResolveLoginMethodAsync(
            loginStartRequest.Email, cancellationToken);

        return Ok(new LoginStartResponseDto(resolvedLoginMethod.Method));
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
