using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Sellevate.Identity.Features.Auth.Models;

namespace Sellevate.Identity.Tests.Helpers;

/// <summary>Mints JWTs matching the Identity service's validation parameters for authenticated test calls.</summary>
public static class JwtTestHelper
{
    public const string JwtKey = "integration-tests-signing-key-which-is-long-enough-0123456789";
    public const string JwtIssuer = "sallevate";
    public const string JwtAudience = "sallevate";

    public static string BuildToken(Guid userId, string email, string displayName, UserRole role = UserRole.User)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim("displayName", displayName),
                new Claim(ClaimTypes.Role, role.ToString())
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
