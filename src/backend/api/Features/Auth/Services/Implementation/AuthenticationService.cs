using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.Auth.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Configuration;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Auth.Services.Implementation;

internal sealed class AuthenticationService(
    AppDbContext databaseContext,
    IOptions<JwtConfiguration> jwtOptions,
    IOptions<GoogleAuthConfiguration> googleOptions,
    ILogger<AuthenticationService> logger) : IAuthenticationService
{
    private const int AccessTokenLifetimeMinutes = 15;
    private const int RefreshTokenLifetimeDays = 30;

    public async Task<IssuedTokenPair> RegisterWithEmailAsync(
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.ToLowerInvariant();
        logger.LogInformation("Registration attempt {Email}", normalizedEmail);

        var existingUser = await databaseContext.Users
            .FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            logger.LogWarning("Registration failed — email already registered {Email}", normalizedEmail);
            throw new InvalidOperationException("Email already registered.");
        }

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow
        };

        databaseContext.Users.Add(newUser);
        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User registered successfully {Email} UserId={UserId}", normalizedEmail, newUser.Id);
        return await IssueTokensForUserAsync(newUser, isOnboardingCompleted: false, cancellationToken);
    }

    public async Task<IssuedTokenPair> LoginWithEmailAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.ToLowerInvariant();
        logger.LogInformation("Login attempt {Email}", normalizedEmail);

        var user = await databaseContext.Users
            .FirstOrDefaultAsync(userRecord => userRecord.Email == normalizedEmail, cancellationToken);

        if (user is null || user.PasswordHash is null ||
            !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            logger.LogWarning("Login failed — invalid credentials {Email}", normalizedEmail);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var isOnboardingCompleted = await databaseContext.UserProfiles
            .AnyAsync(profile => profile.UserId == user.Id && profile.IsOnboardingCompleted, cancellationToken);

        logger.LogInformation("Login successful {Email} UserId={UserId} Role={Role}", normalizedEmail, user.Id, user.Role);
        return await IssueTokensForUserAsync(user, isOnboardingCompleted, cancellationToken);
    }

    public async Task<IssuedTokenPair> LoginWithGoogleAsync(
        string googleIdToken,
        CancellationToken cancellationToken = default)
    {
        var googleClientId = googleOptions.Value.ClientId
            ?? throw new InvalidOperationException("Google:ClientId not configured.");

        var validationSettings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [googleClientId]
        };

        var googlePayload = await GoogleJsonWebSignature.ValidateAsync(
            googleIdToken, validationSettings);

        logger.LogInformation("Google login attempt {Email}", googlePayload.Email);

        var existingUser = await databaseContext.Users
            .FirstOrDefaultAsync(user => user.GoogleId == googlePayload.Subject, cancellationToken)
            ?? await databaseContext.Users
                .FirstOrDefaultAsync(user => user.Email == googlePayload.Email, cancellationToken);

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
            await databaseContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("New user registered via Google {Email} UserId={UserId}", googlePayload.Email, existingUser.Id);
        }
        else if (existingUser.GoogleId is null)
        {
            existingUser.GoogleId = googlePayload.Subject;
            await databaseContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Linked Google account to existing user {Email} UserId={UserId}", googlePayload.Email, existingUser.Id);
        }
        else
        {
            logger.LogInformation("Google login successful {Email} UserId={UserId}", googlePayload.Email, existingUser.Id);
        }

        var isOnboardingCompleted = await databaseContext.UserProfiles
            .AnyAsync(profile => profile.UserId == existingUser.Id && profile.IsOnboardingCompleted, cancellationToken);

        return await IssueTokensForUserAsync(existingUser, isOnboardingCompleted, cancellationToken);
    }

    public async Task<IssuedTokenPair> RefreshAccessTokenAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        var storedToken = await databaseContext.RefreshTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.Token == rawRefreshToken, cancellationToken);

        if (storedToken is null || storedToken.IsRevoked || storedToken.ExpiresAt < DateTime.UtcNow)
        {
            logger.LogWarning("Token refresh failed — invalid or expired refresh token");
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        storedToken.IsRevoked = true;
        await databaseContext.SaveChangesAsync(cancellationToken);

        var isOnboardingCompleted = await databaseContext.UserProfiles
            .AnyAsync(profile => profile.UserId == storedToken.UserId && profile.IsOnboardingCompleted, cancellationToken);

        logger.LogInformation("Access token refreshed for UserId={UserId}", storedToken.UserId);
        return await IssueTokensForUserAsync(storedToken.User, isOnboardingCompleted, cancellationToken);
    }

    public async Task RevokeRefreshTokenAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        var storedToken = await databaseContext.RefreshTokens
            .FirstOrDefaultAsync(token => token.Token == rawRefreshToken, cancellationToken);

        if (storedToken is null) return;

        storedToken.IsRevoked = true;
        await databaseContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Refresh token revoked for UserId={UserId}", storedToken.UserId);
    }

    private async Task<IssuedTokenPair> IssueTokensForUserAsync(
        User user,
        bool isOnboardingCompleted,
        CancellationToken cancellationToken = default)
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
        await databaseContext.SaveChangesAsync(cancellationToken);

        return new IssuedTokenPair(
            AccessToken: accessToken,
            RefreshToken: rawRefreshToken,
            UserId: user.Id.ToString(),
            DisplayName: user.DisplayName,
            IsOnboardingCompleted: isOnboardingCompleted,
            Role: user.Role
        );
    }

    private string BuildJwtAccessToken(User user)
    {
        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtOptions.Value.Key));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("displayName", user.DisplayName),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            ]),
            Expires = DateTime.UtcNow.AddMinutes(AccessTokenLifetimeMinutes),
            Issuer = jwtOptions.Value.Issuer,
            Audience = jwtOptions.Value.Audience,
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
