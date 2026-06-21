using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Sellevate.Analytics.Tests.Helpers;

public static class JwtTestHelper
{
    public const string JwtKey = "integration-tests-signing-key-which-is-long-enough-0123456789";
    public const string JwtIssuer = "sallevate";
    public const string JwtAudience = "sallevate";

    public static string BuildToken(Guid userId)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, "test@example.com"),
            ]),
            Expires = DateTime.UtcNow.AddMinutes(30),
            Issuer = JwtIssuer,
            Audience = JwtAudience,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
