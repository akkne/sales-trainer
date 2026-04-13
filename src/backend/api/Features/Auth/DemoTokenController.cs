using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace SalesTrainer.Api.Features.Auth;

[ApiController]
[Route("demo")]
public sealed class DemoTokenController(IConfiguration configuration) : ControllerBase
{
    [HttpPost("token")]
    public IActionResult IssueDemoToken()
    {
        var demoUserId = Guid.NewGuid();
        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, demoUserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, "demo@salestrainer.app"),
                new Claim("displayName", "Demo User"),
                new Claim("isDemo", "true")
            ]),
            Expires = DateTime.UtcNow.AddHours(2),
            Issuer = configuration["Jwt:Issuer"],
            Audience = configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(
                signingKey, SecurityAlgorithms.HmacSha256)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var accessToken = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));

        return Ok(new { accessToken, expiresInSeconds = 7200 });
    }
}
