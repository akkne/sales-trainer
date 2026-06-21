using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Net;
using System.Text;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sellevate.Identity.Eventing;
using Sellevate.Identity.Features.Auth.Constants;
using Sellevate.Identity.Features.Auth.Exceptions;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Auth.Services.Abstract;
using Sellevate.Identity.Features.Avatars;
using Sellevate.Identity.Infrastructure.Configuration;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.Auth.Services.Implementation;

internal sealed class AuthenticationService(
    IdentityDbContext databaseContext,
    IEmailVerificationService emailVerificationService,
    IUserEventPublisher userEventPublisher,
    IOptions<JwtConfiguration> jwtOptions,
    IOptions<GoogleAuthConfiguration> googleOptions,
    ILogger<AuthenticationService> logger) : IAuthenticationService
{
    public async Task RegisterWithEmailAsync(
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

        var newUserId = Guid.NewGuid();
        var newUser = new User
        {
            Id = newUserId,
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = false,
            DefaultAvatarIndex = DefaultAvatarIndexResolver.Resolve(newUserId, DefaultAvatarSeeder.DefaultAvatarCount)
        };

        databaseContext.Users.Add(newUser);
        try
        {
            await databaseContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            logger.LogWarning("Registration failed — email already registered (unique violation) {Email}", normalizedEmail);
            throw new InvalidOperationException("Email already registered.");
        }

        await PublishUserRegisteredAsync(newUser, cancellationToken);

        try
        {
            await emailVerificationService.GenerateAndSendCodeAsync(normalizedEmail, displayName, cancellationToken);
        }
        catch (Exception verificationEmailException)
        {
            logger.LogError(
                verificationEmailException,
                "User {Email} UserId={UserId} registered but the verification email failed to send; client must use resend-code",
                normalizedEmail, newUser.Id);
        }

        logger.LogInformation(
            "User registered pending email verification {Email} UserId={UserId}", normalizedEmail, newUser.Id);
    }

    public async Task<IssuedTokenPair> VerifyEmailAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.ToLowerInvariant();

        var user = await databaseContext.Users
            .FirstOrDefaultAsync(userRecord => userRecord.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            logger.LogWarning("Email verification failed — unknown email {Email}", normalizedEmail);
            throw new UnauthorizedAccessException(EmailVerificationConstants.InvalidCodeMessage);
        }

        var isCodeValid = await emailVerificationService.VerifyCodeAsync(normalizedEmail, code, cancellationToken);
        if (!isCodeValid)
        {
            throw new UnauthorizedAccessException(EmailVerificationConstants.InvalidCodeMessage);
        }

        if (!user.IsEmailVerified)
        {
            user.IsEmailVerified = true;
            await databaseContext.SaveChangesAsync(cancellationToken);
        }

        var isOnboardingCompleted = await databaseContext.UserProfiles
            .AnyAsync(profile => profile.UserId == user.Id && profile.IsOnboardingCompleted, cancellationToken);

        logger.LogInformation("Email verified {Email} UserId={UserId}", normalizedEmail, user.Id);
        return await IssueTokensForUserAsync(user, isOnboardingCompleted, cancellationToken);
    }

    public async Task ResendVerificationCodeAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.ToLowerInvariant();

        var user = await databaseContext.Users
            .FirstOrDefaultAsync(userRecord => userRecord.Email == normalizedEmail, cancellationToken);

        if (user is null || user.IsEmailVerified)
        {
            logger.LogInformation(
                "Resend verification code ignored for {Email} (unknown or already verified)", normalizedEmail);
            return;
        }

        await emailVerificationService.GenerateAndSendCodeAsync(normalizedEmail, user.DisplayName, cancellationToken);
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

        if (!user.IsEmailVerified)
        {
            logger.LogWarning("Login blocked — email not verified {Email}", normalizedEmail);
            throw new EmailNotVerifiedException(normalizedEmail);
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

        if (!googlePayload.EmailVerified)
        {
            logger.LogWarning("Google login rejected — email not verified by Google {Email}", googlePayload.Email);
            throw new UnauthorizedAccessException("Google account email is not verified.");
        }

        logger.LogInformation("Google login attempt {Email}", googlePayload.Email);

        // First try to find by GoogleId; only fall back to email match when the local account is also email-verified.
        var existingUser = await databaseContext.Users
            .FirstOrDefaultAsync(user => user.GoogleId == googlePayload.Subject, cancellationToken);

        if (existingUser is null)
        {
            var emailMatchedUser = await databaseContext.Users
                .FirstOrDefaultAsync(user => user.Email == googlePayload.Email, cancellationToken);

            if (emailMatchedUser is not null && emailMatchedUser.IsEmailVerified)
            {
                existingUser = emailMatchedUser;
            }
        }

        var isNewUser = false;
        var displayName = string.IsNullOrWhiteSpace(googlePayload.Name)
            ? googlePayload.Email
            : googlePayload.Name;

        if (existingUser is null)
        {
            var newGoogleUserId = Guid.NewGuid();
            existingUser = new User
            {
                Id = newGoogleUserId,
                Email = googlePayload.Email,
                DisplayName = displayName,
                GoogleId = googlePayload.Subject,
                CreatedAt = DateTime.UtcNow,
                IsEmailVerified = true,
                DefaultAvatarIndex = DefaultAvatarIndexResolver.Resolve(newGoogleUserId, DefaultAvatarSeeder.DefaultAvatarCount)
            };
            databaseContext.Users.Add(existingUser);
            await databaseContext.SaveChangesAsync(cancellationToken);
            isNewUser = true;
            logger.LogInformation("New user registered via Google {Email} UserId={UserId}", googlePayload.Email, existingUser.Id);
        }
        else if (existingUser.GoogleId is null)
        {
            existingUser.GoogleId = googlePayload.Subject;
            existingUser.IsEmailVerified = true;
            await databaseContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Linked Google account to existing user {Email} UserId={UserId}", googlePayload.Email, existingUser.Id);
        }
        else
        {
            logger.LogInformation("Google login successful {Email} UserId={UserId}", googlePayload.Email, existingUser.Id);
        }

        if (isNewUser)
        {
            await PublishUserRegisteredAsync(existingUser, cancellationToken);
        }

        var isOnboardingCompleted = await databaseContext.UserProfiles
            .AnyAsync(profile => profile.UserId == existingUser.Id && profile.IsOnboardingCompleted, cancellationToken);

        return await IssueTokensForUserAsync(existingUser, isOnboardingCompleted, cancellationToken);
    }

    public async Task<IssuedTokenPair> RefreshAccessTokenAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = ComputeTokenHash(rawRefreshToken);

        // Load the token regardless of IsRevoked so we can detect reuse.
        var storedToken = await databaseContext.RefreshTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.Token == tokenHash, cancellationToken);

        if (storedToken is null || storedToken.ExpiresAt < DateTime.UtcNow)
        {
            logger.LogWarning("Token refresh failed — invalid or expired refresh token");
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        // Atomic conditional revocation: only succeeds if the row is still NOT revoked.
        var affected = await databaseContext.RefreshTokens
            .Where(token => token.Id == storedToken.Id && !token.IsRevoked)
            .ExecuteUpdateAsync(
                update => update.SetProperty(token => token.IsRevoked, true),
                cancellationToken);

        if (affected == 0)
        {
            // Reuse detected — revoke the whole family.
            logger.LogWarning(
                "Refresh-token reuse detected for UserId={UserId}; revoking all active refresh tokens",
                storedToken.UserId);
            await databaseContext.RefreshTokens
                .Where(token => token.UserId == storedToken.UserId && !token.IsRevoked)
                .ExecuteUpdateAsync(
                    update => update.SetProperty(token => token.IsRevoked, true),
                    cancellationToken);
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        var isOnboardingCompleted = await databaseContext.UserProfiles
            .AnyAsync(profile => profile.UserId == storedToken.UserId && profile.IsOnboardingCompleted, cancellationToken);

        logger.LogInformation("Access token refreshed for UserId={UserId}", storedToken.UserId);
        return await IssueTokensForUserAsync(storedToken.User, isOnboardingCompleted, cancellationToken);
    }

    public async Task RevokeRefreshTokenAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = ComputeTokenHash(rawRefreshToken);

        var storedToken = await databaseContext.RefreshTokens
            .FirstOrDefaultAsync(token => token.Token == tokenHash, cancellationToken);

        if (storedToken is null)
        {
            return;
        }

        storedToken.IsRevoked = true;
        await databaseContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Refresh token revoked for UserId={UserId}", storedToken.UserId);
    }

    private Task PublishUserRegisteredAsync(User user, CancellationToken cancellationToken) =>
        userEventPublisher.PublishRegisteredAsync(
            new UserRegisteredEvent(user.Id, user.Email, user.DisplayName, user.AvatarKey),
            cancellationToken);

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
            Token = ComputeTokenHash(rawRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(jwtOptions.Value.RefreshTokenLifetimeDays),
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
            Expires = DateTime.UtcNow.AddMinutes(jwtOptions.Value.AccessTokenLifetimeMinutes),
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

    private static string ComputeTokenHash(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes);
    }
}
