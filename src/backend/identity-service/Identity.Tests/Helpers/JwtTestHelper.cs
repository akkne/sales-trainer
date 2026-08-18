using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Sellevate.Identity.Features.Auth.Models;

namespace Sellevate.Identity.Tests.Helpers;

public static class JwtTestHelper
{
    public const string JwtKey = "integration-tests-signing-key-which-is-long-enough-0123456789";
    public const string JwtIssuer = "sallevate";
    public const string JwtAudience = "sallevate";

    public static string BuildToken(
        Guid userId,
        string email,
        string displayName,
        UserRole role = UserRole.User,
        Guid? organizationId = null,
        string? orgRole = null)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new("displayName", displayName),
            new(ClaimTypes.Role, role.ToString())
        };

        if (organizationId is not null)
        {
            claims.Add(new Claim("org_id", organizationId.Value.ToString()));
        }

        if (orgRole is not null)
        {
            claims.Add(new Claim("org_role", orgRole));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(30),
            Issuer = JwtIssuer,
            Audience = JwtAudience,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
