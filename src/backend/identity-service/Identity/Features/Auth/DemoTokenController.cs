using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Common.Constants;
using Sellevate.Identity.Infrastructure.Configuration;

namespace Sellevate.Identity.Features.Auth;

/// <summary>
/// Mints an unauthenticated throwaway token so the frontend can be exercised without an account. Every
/// route answers 404 in Production — the guard is the whole reason this controller can exist at all, and
/// the token it issues carries no role and no organization. <see cref="DemoCallers.IsDemoClaimType"/> is
/// what lets that token still pass a <c>[TenantScoped]</c> route downstream (docs/AUDIT_NIGHT_REVIEW.md
/// R-18) — read from the same constant it is minted with, rather than a second copy of the string.
/// </summary>
[ApiController]
[Route("demo")]
public sealed class DemoTokenController(
    IOptions<JwtConfiguration> jwtOptions,
    IWebHostEnvironment environment) : ControllerBase
{
    private const string DemoUserEmail = "demo@salestrainer.app";
    private const string DemoUserDisplayName = "Demo User";
    private const int SecondsPerHour = 3600;

    [HttpPost("token")]
    public IActionResult IssueDemoToken()
    {
        if (environment.IsProduction())
        {
            return NotFound();
        }

        var demoUserId = Guid.NewGuid();
        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtOptions.Value.Key));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, demoUserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, DemoUserEmail),
                new Claim(ClaimTypeNames.DisplayName, DemoUserDisplayName),
                new Claim(DemoCallers.IsDemoClaimType, bool.TrueString.ToLowerInvariant())
            ]),
            Expires = DateTime.UtcNow.AddHours(jwtOptions.Value.DemoTokenLifetimeHours),
            Issuer = jwtOptions.Value.Issuer,
            Audience = jwtOptions.Value.Audience,
            SigningCredentials = new SigningCredentials(
                signingKey, SecurityAlgorithms.HmacSha256)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var accessToken = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));

        return Ok(new { accessToken, expiresInSeconds = jwtOptions.Value.DemoTokenLifetimeHours * SecondsPerHour });
    }
}
