using Microsoft.AspNetCore.Mvc;

namespace SalesTrainer.Api.Features.Auth;

[ApiController]
[Route("auth")]
public class AuthController(AuthenticationService authenticationService) : ControllerBase
{
    private const string RefreshTokenCookieName = "refreshToken";
    private static readonly CookieOptions SecureHttpOnlyCookieOptions = new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        MaxAge = TimeSpan.FromDays(30)
    };

    [HttpPost("register")]
    public async Task<ActionResult<AuthTokenResponseDto>> RegisterWithEmail(
        [FromBody] RegisterRequestDto registerRequest)
    {
        try
        {
            var issuedTokenPair = await authenticationService.RegisterWithEmailAsync(
                registerRequest.Email,
                registerRequest.Password,
                registerRequest.DisplayName);

            return OkWithRefreshTokenCookie(issuedTokenPair);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthTokenResponseDto>> LoginWithEmail(
        [FromBody] LoginRequestDto loginRequest)
    {
        try
        {
            var issuedTokenPair = await authenticationService.LoginWithEmailAsync(
                loginRequest.Email,
                loginRequest.Password);

            return OkWithRefreshTokenCookie(issuedTokenPair);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Unauthorized(new { message = exception.Message });
        }
    }

    [HttpPost("google")]
    public async Task<ActionResult<AuthTokenResponseDto>> LoginWithGoogle(
        [FromBody] GoogleLoginRequestDto googleLoginRequest)
    {
        try
        {
            var issuedTokenPair = await authenticationService.LoginWithGoogleAsync(
                googleLoginRequest.IdToken);

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
    public async Task<ActionResult<AuthTokenResponseDto>> RefreshAccessToken()
    {
        var rawRefreshToken = Request.Cookies[RefreshTokenCookieName];

        if (string.IsNullOrEmpty(rawRefreshToken))
            return Unauthorized(new { message = "Refresh token missing." });

        try
        {
            var issuedTokenPair = await authenticationService.RefreshAccessTokenAsync(rawRefreshToken);
            return OkWithRefreshTokenCookie(issuedTokenPair);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Unauthorized(new { message = exception.Message });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var rawRefreshToken = Request.Cookies[RefreshTokenCookieName];

        if (!string.IsNullOrEmpty(rawRefreshToken))
            await authenticationService.RevokeRefreshTokenAsync(rawRefreshToken);

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
            IsOnboardingCompleted: issuedTokenPair.IsOnboardingCompleted));
    }
}
