using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SalesTrainer.Api.Infrastructure.Configuration;

namespace SalesTrainer.Api.Features.Auth;

[ApiController]
[Route("demo")]
public sealed class DemoTokenController(IOptions<JwtConfiguration> jwtOptions) : ControllerBase
{
    [HttpPost("token")]
    public IActionResult IssueDemoToken()
    {
        var demoUserId = Guid.NewGuid();
        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtOptions.Value.Key));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, demoUserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, "demo@salestrainer.app"),
                new Claim("displayName", "Demo User"),
                new Claim("isDemo", "true")
            ]),
            Expires = DateTime.UtcNow.AddHours(jwtOptions.Value.DemoTokenLifetimeHours),
            Issuer = jwtOptions.Value.Issuer,
            Audience = jwtOptions.Value.Audience,
            SigningCredentials = new SigningCredentials(
                signingKey, SecurityAlgorithms.HmacSha256)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var accessToken = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));

        return Ok(new { accessToken, expiresInSeconds = jwtOptions.Value.DemoTokenLifetimeHours * 3600 });
    }
}
