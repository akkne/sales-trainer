using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Identity.Features.Auth.Constants;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Auth.Services.Abstract;
using Sellevate.Identity.Features.Auth.Services.Implementation;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Infrastructure.Configuration;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Unit;

[TestFixture]
public class AuthenticationServiceSecurityTests
{
    private static readonly IOptions<JwtConfiguration> JwtOptions = Options.Create(new JwtConfiguration
    {
        Key = "super-secret-test-key-that-is-at-least-32-bytes",
        Issuer = "test",
        Audience = "test",
        AccessTokenLifetimeMinutes = 15,
        RefreshTokenLifetimeDays = 7,
        DemoTokenLifetimeHours = 1
    });

    // Phase 40.8: the resolver and the provider collection are wired with their real
    // implementations rather than substitutes — the whole point of the seam is which provider the
    // resolved method dispatches to, and a stub would assert nothing about that.
    private static AuthenticationService BuildService(
        IdentityDbContext db,
        IGoogleTokenValidator? googleTokenValidator = null) =>
        new(
            db,
            Substitute.For<IEmailVerificationService>(),
            googleTokenValidator ?? Substitute.For<IGoogleTokenValidator>(),
            new OrganizationAuthConfigurationResolver(
                db, NullLogger<OrganizationAuthConfigurationResolver>.Instance),
            [new PasswordAuthProvider(db, NullLogger<PasswordAuthProvider>.Instance)],
            JwtOptions,
            NullLogger<AuthenticationService>.Instance);

    private static string ComputeTokenHash(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes);
    }

    // ── ID5: Refresh tokens stored as SHA-256 hash ────────────────────────────

    [Test]
    public async Task LoginWithEmail_StoresHashedRefreshToken_NotRawToken()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "hash@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1!"),
            DisplayName = "Hash User",
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = BuildService(db);
        var result = await service.LoginWithEmailAsync("hash@test.com", "Password1!");

        var rawToken = result.RefreshToken;
        var expectedHash = ComputeTokenHash(rawToken);

        var storedToken = await db.RefreshTokens.FirstOrDefaultAsync(t => t.UserId == user.Id);
        storedToken.Should().NotBeNull();
        storedToken!.Token.Should().Be(expectedHash, "only the SHA-256 hash should be persisted");
        storedToken.Token.Should().NotBe(rawToken, "raw token must not be stored in plaintext");
    }

    [Test]
    public async Task LoginWithEmail_StoredTokenHash_CanBeVerifiedByReHashingRawToken()
    {
        // This test verifies the hash round-trip without calling RefreshAccessTokenAsync
        // (which uses ExecuteUpdateAsync, unsupported by the EF in-memory provider).
        await using var db = InMemoryDbContextFactory.Create();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "refresh@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1!"),
            DisplayName = "Refresh User",
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = BuildService(db);
        var loginResult = await service.LoginWithEmailAsync("refresh@test.com", "Password1!");
        var rawToken = loginResult.RefreshToken;

        // The stored hash must equal SHA-256(rawToken), making lookup by hash possible.
        var expectedHash = ComputeTokenHash(rawToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.UserId == user.Id);
        stored.Should().NotBeNull();
        stored!.Token.Should().Be(expectedHash, "service must look up tokens by their SHA-256 hash");
    }

    [Test]
    public async Task RefreshAccessToken_WithHashDirectly_Fails()
    {
        // Passing the hash as if it were the raw token must fail (double-hash → not found).
        // Uses expired/missing path — ExecuteUpdateAsync not exercised.
        await using var db = InMemoryDbContextFactory.Create();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "hashfail@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1!"),
            DisplayName = "Hash Fail User",
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = BuildService(db);
        var loginResult = await service.LoginWithEmailAsync("hashfail@test.com", "Password1!");
        var rawToken = loginResult.RefreshToken;
        var hash = ComputeTokenHash(rawToken);

        // Double-hashing means the stored record won't be found → UnauthorizedException.
        var act = async () => await service.RefreshAccessTokenAsync(hash);
        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "passing the hash as the raw token should not find the stored record (double-hash)");
    }

    // ── ID4: Atomic rotation — revoked token triggers family revocation ───────
    // NOTE: RefreshAccessToken_AlreadyRevokedToken_ThrowsAndRevokesFamily requires
    // ExecuteUpdateAsync which is not supported by the EF in-memory provider.
    // This scenario is covered by the integration test suite (AuthFlowTests) which
    // runs against a real PostgreSQL container.

    // ── ID6: Google login — EmailVerified guard ───────────────────────────────

    [Test]
    public async Task LoginWithGoogle_UnverifiedEmail_ThrowsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var service = BuildService(db, StubGoogleValidator("unverified@test.com", isEmailVerified: false));

        var act = async () => await service.LoginWithGoogleAsync("google-id-token");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── 40.7: Google login is invite-only — no implicit account creation ──────

    [Test]
    public async Task LoginWithGoogle_UnknownEmail_ThrowsAndCreatesNoUser()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var service = BuildService(db, StubGoogleValidator("stranger@test.com"));

        var act = async () => await service.LoginWithGoogleAsync("google-id-token");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await db.Users.CountAsync()).Should().Be(0, "Google sign-in must never provision an account");
    }

    [Test]
    public async Task LoginWithGoogle_ExistingUserWithoutMembership_ThrowsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        await SeedGoogleUserAsync(db, "no-membership@test.com");
        var service = BuildService(db, StubGoogleValidator("no-membership@test.com"));

        var act = async () => await service.LoginWithGoogleAsync("google-id-token");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public async Task LoginWithGoogle_ExistingUserWithDeactivatedMembership_ThrowsUnauthorized()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var user = await SeedGoogleUserAsync(db, "deactivated@test.com");
        db.Memberships.Add(new Membership
        {
            UserId = user.Id,
            OrganizationId = Guid.NewGuid(),
            Role = OrgRole.Manager,
            Status = MembershipStatus.Deactivated,
            JoinedAt = DateTime.UtcNow.AddDays(-10),
            DeactivatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = BuildService(db, StubGoogleValidator("deactivated@test.com"));

        var act = async () => await service.LoginWithGoogleAsync("google-id-token");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public async Task LoginWithGoogle_ExistingUserWithActiveMembership_IssuesTokensWithOrgClaims()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var user = await SeedGoogleUserAsync(db, "member@test.com");
        var organizationId = Guid.NewGuid();
        db.Memberships.Add(new Membership
        {
            UserId = user.Id,
            OrganizationId = organizationId,
            Role = OrgRole.TenancyAdmin,
            Status = MembershipStatus.Active,
            JoinedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = BuildService(db, StubGoogleValidator("member@test.com"));

        var issuedTokenPair = await service.LoginWithGoogleAsync("google-id-token");

        issuedTokenPair.OrgId.Should().Be(organizationId.ToString());
        issuedTokenPair.OrgRole.Should().Be("TenancyAdmin");
    }

    // ── 40.8: the login method is per-organization configuration ──────────────

    /// <summary>
    /// The behaviour that makes the seam more than a shape: an organization configured for a
    /// method nobody implements is refused, not quietly downgraded to a password. Without this,
    /// switching a customer to SSO would leave their password login silently working alongside it.
    /// </summary>
    [Test]
    public async Task LoginWithEmail_ForOrganizationConfiguredForSso_IsRejectedDespiteCorrectPassword()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var organizationId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "employee@bigcustomer.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1!"),
            DisplayName = "Employee",
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = true
        };
        db.Users.Add(user);
        db.Memberships.Add(new Membership
        {
            UserId = user.Id,
            OrganizationId = organizationId,
            Role = OrgRole.Manager,
            Status = MembershipStatus.Active,
            JoinedAt = DateTime.UtcNow
        });
        db.OrganizationAuthConfigurations.Add(new OrganizationAuthConfiguration
        {
            OrganizationId = organizationId,
            Method = AuthMethodNames.Oidc,
            AllowedEmailDomains = ["bigcustomer.test"],
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = BuildService(db);

        var act = async () => await service.LoginWithEmailAsync("employee@bigcustomer.test", "Password1!");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await db.RefreshTokens.CountAsync()).Should().Be(0, "no session may be issued for a refused method");
    }

    [Test]
    public async Task ResolveLoginMethodAsync_ForAnUnknownAddress_StillAnswersPassword()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var service = BuildService(db);

        var resolved = await service.ResolveLoginMethodAsync("stranger@nowhere.test");

        resolved.Method.Should().Be(AuthMethodNames.Password);
    }

    private static IGoogleTokenValidator StubGoogleValidator(string email, bool isEmailVerified = true)
    {
        var validator = Substitute.For<IGoogleTokenValidator>();
        validator.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GoogleUserPayload($"google-subject-{email}", email, "Google User", isEmailVerified));
        return validator;
    }

    private static async Task<User> SeedGoogleUserAsync(IdentityDbContext db, string email)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "Google User",
            GoogleId = $"google-subject-{email}",
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
