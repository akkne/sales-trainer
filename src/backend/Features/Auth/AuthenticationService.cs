using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Auth;

public class AuthenticationService(AppDbContext databaseContext, IConfiguration configuration)
{
    private const int AccessTokenLifetimeMinutes = 15;
    private const int RefreshTokenLifetimeDays = 30;

    public async Task<IssuedTokenPair> RegisterWithEmailAsync(
        string email,
        string password,
        string displayName)
    {
        var existingUser = await databaseContext.Users
            .FirstOrDefaultAsync(user => user.Email == email.ToLowerInvariant());

        if (existingUser is not null)
            throw new InvalidOperationException("Email already registered.");

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow
        };

        databaseContext.Users.Add(newUser);
        await databaseContext.SaveChangesAsync();

        return await IssueTokensForUserAsync(newUser, isOnboardingCompleted: false);
    }

    public async Task<IssuedTokenPair> LoginWithEmailAsync(string email, string password)
    {
        var user = await databaseContext.Users
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());

        if (user is null || user.PasswordHash is null ||
            !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        var isOnboardingCompleted = await databaseContext.UserProfiles
            .AnyAsync(profile => profile.UserId == user.Id && profile.IsOnboardingCompleted);

        return await IssueTokensForUserAsync(user, isOnboardingCompleted);
    }

    public async Task<IssuedTokenPair> LoginWithGoogleAsync(string googleIdToken)
    {
        var googleClientId = configuration["Google:ClientId"]
            ?? throw new InvalidOperationException("Google:ClientId not configured.");

        var validationSettings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [googleClientId]
        };

        var googlePayload = await GoogleJsonWebSignature.ValidateAsync(
            googleIdToken, validationSettings);

        var existingUser = await databaseContext.Users
            .FirstOrDefaultAsync(user => user.GoogleId == googlePayload.Subject)
            ?? await databaseContext.Users
                .FirstOrDefaultAsync(user => user.Email == googlePayload.Email);

        if (existingUser is null)
        {
            existingUser = new User
            {
                Id = Guid.NewGuid(),
                Email = googlePayload.Email,
                DisplayName = googlePayload.Name,
                GoogleId = googlePayload.Subject,
                CreatedAt = DateTime.UtcNow
            };
            databaseContext.Users.Add(existingUser);
            await databaseContext.SaveChangesAsync();
        }
        else if (existingUser.GoogleId is null)
        {
            existingUser.GoogleId = googlePayload.Subject;
            await databaseContext.SaveChangesAsync();
        }

        var isOnboardingCompleted = await databaseContext.UserProfiles
            .AnyAsync(profile => profile.UserId == existingUser.Id && profile.IsOnboardingCompleted);

        return await IssueTokensForUserAsync(existingUser, isOnboardingCompleted);
    }

    public async Task<IssuedTokenPair> RefreshAccessTokenAsync(string rawRefreshToken)
    {
        var storedToken = await databaseContext.RefreshTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.Token == rawRefreshToken);

        if (storedToken is null || storedToken.IsRevoked || storedToken.ExpiresAt < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        storedToken.IsRevoked = true;
        await databaseContext.SaveChangesAsync();

        var isOnboardingCompleted = await databaseContext.UserProfiles
            .AnyAsync(profile => profile.UserId == storedToken.UserId && profile.IsOnboardingCompleted);

        return await IssueTokensForUserAsync(storedToken.User, isOnboardingCompleted);
    }

    public async Task RevokeRefreshTokenAsync(string rawRefreshToken)
    {
        var storedToken = await databaseContext.RefreshTokens
            .FirstOrDefaultAsync(token => token.Token == rawRefreshToken);

        if (storedToken is null) return;

        storedToken.IsRevoked = true;
        await databaseContext.SaveChangesAsync();
    }

    private async Task<IssuedTokenPair> IssueTokensForUserAsync(
        User user,
        bool isOnboardingCompleted)
    {
        var accessToken = BuildJwtAccessToken(user);
        var rawRefreshToken = GenerateSecureRandomToken();

        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = rawRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenLifetimeDays),
            IsRevoked = false
        };

        databaseContext.RefreshTokens.Add(newRefreshToken);
        await databaseContext.SaveChangesAsync();

        return new IssuedTokenPair(
            AccessToken: accessToken,
            RefreshToken: rawRefreshToken,
            UserId: user.Id.ToString(),
            DisplayName: user.DisplayName,
            IsOnboardingCompleted: isOnboardingCompleted
        );
    }

    private string BuildJwtAccessToken(User user)
    {
        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("displayName", user.DisplayName)
            ]),
            Expires = DateTime.UtcNow.AddMinutes(AccessTokenLifetimeMinutes),
            Issuer = configuration["Jwt:Issuer"],
            Audience = configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(
                signingKey, SecurityAlgorithms.HmacSha256)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(securityToken);
    }

    private static string GenerateSecureRandomToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }
}
