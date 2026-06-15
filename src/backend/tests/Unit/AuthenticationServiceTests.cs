using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using SalesTrainer.Api.Features.Auth.Exceptions;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.Auth.Services.Implementation;
using SalesTrainer.Api.Infrastructure.Configuration;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Unit;

[TestFixture]
public class AuthenticationServiceTests
{
    private AppDbContext _db = null!;
    private RecordingEmailSender _emailSender = null!;
    private EmailVerificationService _emailVerificationService = null!;
    private AuthenticationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _db = InMemoryDbContextFactory.Create();

        var jwtOptions = Options.Create(new JwtConfiguration
        {
            Key = "super-secret-test-key-that-is-at-least-32-chars-long!",
            Issuer = "test-issuer",
            Audience = "test-audience"
        });
        var googleOptions = Options.Create(new GoogleAuthConfiguration
        {
            ClientId = "test-client-id"
        });
        var emailVerificationOptions = Options.Create(new EmailVerificationConfiguration());

        _emailSender = new RecordingEmailSender();
        _emailVerificationService = new EmailVerificationService(
            _db,
            _emailSender,
            emailVerificationOptions,
            NullLogger<EmailVerificationService>.Instance);

        _service = new AuthenticationService(
            _db,
            _emailVerificationService,
            jwtOptions,
            googleOptions,
            NullLogger<AuthenticationService>.Instance);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task Register_NewEmail_CreatesUnverifiedUserAndSendsCode()
    {
        await _service.RegisterWithEmailAsync(
            "new@example.com", "Password123!", "New User");

        var user = _db.Users.FirstOrDefault(u => u.Email == "new@example.com");
        user.Should().NotBeNull();
        user!.IsEmailVerified.Should().BeFalse();
        _emailSender.GetLastCodeFor("new@example.com").Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task VerifyEmail_CorrectCode_MarksVerifiedAndReturnsTokenPair()
    {
        await _service.RegisterWithEmailAsync(
            "verify@example.com", "Password123!", "Verify User");
        var code = _emailSender.GetLastCodeFor("verify@example.com")!;

        var result = await _service.VerifyEmailAsync("verify@example.com", code);

        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();

        var user = _db.Users.First(u => u.Email == "verify@example.com");
        user.IsEmailVerified.Should().BeTrue();
    }

    [Test]
    public async Task VerifyEmail_WrongCode_ThrowsUnauthorizedAccessException()
    {
        await _service.RegisterWithEmailAsync(
            "wrongcode@example.com", "Password123!", "Wrong Code User");

        var act = async () => await _service.VerifyEmailAsync("wrongcode@example.com", "000000");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public async Task Register_DuplicateEmail_ThrowsInvalidOperationException()
    {
        await _db.Users.AddAsync(new User
        {
            Id = Guid.NewGuid(),
            Email = "dup@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            DisplayName = "Existing",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var act = async () => await _service.RegisterWithEmailAsync(
            "dup@example.com", "Password123!", "Duplicate");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task Register_EmailNormalizedToLowercase()
    {
        await _service.RegisterWithEmailAsync(
            "UPPER@Example.COM", "Password123!", "Upper User");

        var user = _db.Users.FirstOrDefault(u => u.Email == "upper@example.com");
        user.Should().NotBeNull();
    }

    [Test]
    public async Task Login_ValidCredentials_ReturnsTokenPair()
    {
        await _db.Users.AddAsync(new User
        {
            Id = Guid.NewGuid(),
            Email = "login@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            DisplayName = "Login User",
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = true
        });
        await _db.SaveChangesAsync();

        var result = await _service.LoginWithEmailAsync("login@example.com", "Password123!");

        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task Login_UnverifiedEmail_ThrowsEmailNotVerifiedException()
    {
        await _db.Users.AddAsync(new User
        {
            Id = Guid.NewGuid(),
            Email = "unverified@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            DisplayName = "Unverified User",
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = false
        });
        await _db.SaveChangesAsync();

        var act = async () =>
            await _service.LoginWithEmailAsync("unverified@example.com", "Password123!");

        await act.Should().ThrowAsync<EmailNotVerifiedException>();
    }

    [Test]
    public async Task Login_UnknownEmail_ThrowsUnauthorizedAccessException()
    {
        var act = async () =>
            await _service.LoginWithEmailAsync("nobody@example.com", "Password123!");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public async Task Login_WrongPassword_ThrowsUnauthorizedAccessException()
    {
        await _db.Users.AddAsync(new User
        {
            Id = Guid.NewGuid(),
            Email = "wp@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword!"),
            DisplayName = "WP User",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var act = async () =>
            await _service.LoginWithEmailAsync("wp@example.com", "WrongPassword!");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public async Task Refresh_ValidToken_RevokesOldIssuesNew()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "refresh@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            DisplayName = "Refresh User",
            CreatedAt = DateTime.UtcNow
        };
        var oldToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Token = "valid-refresh-token",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsRevoked = false
        };
        _db.Users.Add(user);
        _db.RefreshTokens.Add(oldToken);
        await _db.SaveChangesAsync();

        var result = await _service.RefreshAccessTokenAsync("valid-refresh-token");

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBe("valid-refresh-token");

        var revokedToken = _db.RefreshTokens.First(t => t.Token == "valid-refresh-token");
        revokedToken.IsRevoked.Should().BeTrue();
    }

    [Test]
    public async Task Refresh_ExpiredToken_ThrowsUnauthorizedAccessException()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "expired@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            DisplayName = "Expired User",
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Token = "expired-token",
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            IsRevoked = false
        });
        await _db.SaveChangesAsync();

        var act = async () => await _service.RefreshAccessTokenAsync("expired-token");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public async Task Refresh_RevokedToken_ThrowsUnauthorizedAccessException()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "revoked@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            DisplayName = "Revoked User",
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Token = "revoked-token",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsRevoked = true
        });
        await _db.SaveChangesAsync();

        var act = async () => await _service.RefreshAccessTokenAsync("revoked-token");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public async Task Revoke_ExistingToken_SetsIsRevokedTrue()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "revoke@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            DisplayName = "Revoke User",
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Token = "token-to-revoke",
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsRevoked = false
        });
        await _db.SaveChangesAsync();

        await _service.RevokeRefreshTokenAsync("token-to-revoke");

        var token = _db.RefreshTokens.First(t => t.Token == "token-to-revoke");
        token.IsRevoked.Should().BeTrue();
    }
}
