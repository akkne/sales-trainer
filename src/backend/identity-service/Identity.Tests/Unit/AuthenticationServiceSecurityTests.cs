using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Identity.Eventing;
using Sellevate.Identity.Features.Auth.Constants;
using Sellevate.Identity.Features.Auth.Exceptions;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Auth.Services.Abstract;
using Sellevate.Identity.Features.Auth.Services.Implementation;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Infrastructure.Configuration;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Unit;

/// <summary>
/// The security invariants of token issuance: a refresh token is only ever persisted as its SHA-256
/// hash, and no self-service path — password sign-up or Google — ever hands out a membership. Since
/// 40.37 both of them do provision an <em>account</em>; the invariant that survived 40.7 is the
/// narrower and more important one, that an account is not access to anybody's organization.
///
/// <para>
/// <c>RefreshAccessToken_AlreadyRevokedToken_ThrowsAndRevokesFamily</c> is deliberately absent here:
/// the atomic rotation it would exercise uses <c>ExecuteUpdateAsync</c>, which the EF in-memory
/// provider does not implement. That scenario lives in <c>AuthFlowTests</c>, which runs against a real
/// PostgreSQL container.
/// </para>
/// </summary>
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

    /// <summary>
    /// Phase 40.8: the resolver and the provider collection are wired with their real implementations
    /// rather than substitutes — the whole point of the seam is which provider the resolved method
    /// dispatches to, and a stub would assert nothing about that.
    /// </summary>
    private static AuthenticationService BuildService(
        IdentityDbContext databaseContext,
        IGoogleTokenValidator? googleTokenValidator = null,
        IUserEventPublisher? userEventPublisher = null,
        IEmailVerificationService? emailVerificationService = null,
        bool isEmailVerificationEnabled = false) =>
        new(
            databaseContext,
            emailVerificationService ?? Substitute.For<IEmailVerificationService>(),
            googleTokenValidator ?? Substitute.For<IGoogleTokenValidator>(),
            new OrganizationAuthConfigurationResolver(
                databaseContext, NullLogger<OrganizationAuthConfigurationResolver>.Instance),
            [new PasswordAuthProvider(databaseContext, NullLogger<PasswordAuthProvider>.Instance)],
            userEventPublisher ?? Substitute.For<IUserEventPublisher>(),
            JwtOptions,
            Options.Create(new EmailVerificationConfiguration { Enabled = isEmailVerificationEnabled }),
            NullLogger<AuthenticationService>.Instance);

    private static string ComputeTokenHash(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes);
    }

    [Test]
    public async Task LoginWithEmail_StoresHashedRefreshToken_NotRawToken()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "hash@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1!"),
            DisplayName = "Hash User",
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = true
        };
        databaseContext.Users.Add(user);
        await databaseContext.SaveChangesAsync();

        var service = BuildService(databaseContext);
        var result = await service.LoginWithEmailAsync("hash@test.com", "Password1!");

        var rawToken = result.RefreshToken;
        var expectedHash = ComputeTokenHash(rawToken);

        var storedToken = await databaseContext.RefreshTokens.FirstOrDefaultAsync(t => t.UserId == user.Id);
        storedToken.Should().NotBeNull();
        storedToken!.Token.Should().Be(expectedHash, "only the SHA-256 hash should be persisted");
        storedToken.Token.Should().NotBe(rawToken, "raw token must not be stored in plaintext");
    }

    /// <summary>
    /// Verifies the hash round-trip without calling <c>RefreshAccessTokenAsync</c>, which uses
    /// <c>ExecuteUpdateAsync</c> and is unsupported by the EF in-memory provider. The stored hash must
    /// equal <c>SHA-256(rawToken)</c>, which is what makes lookup by hash possible at all.
    /// </summary>
    [Test]
    public async Task LoginWithEmail_StoredTokenHash_CanBeVerifiedByReHashingRawToken()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "refresh@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1!"),
            DisplayName = "Refresh User",
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = true
        };
        databaseContext.Users.Add(user);
        await databaseContext.SaveChangesAsync();

        var service = BuildService(databaseContext);
        var loginResult = await service.LoginWithEmailAsync("refresh@test.com", "Password1!");
        var rawToken = loginResult.RefreshToken;

        var expectedHash = ComputeTokenHash(rawToken);
        var stored = await databaseContext.RefreshTokens.FirstOrDefaultAsync(t => t.UserId == user.Id);
        stored.Should().NotBeNull();
        stored!.Token.Should().Be(expectedHash, "service must look up tokens by their SHA-256 hash");
    }

    /// <summary>
    /// Passing the stored hash as if it were the raw token must fail: the service hashes what it is
    /// given, so a double hash matches nothing. Exercises the expired-or-missing path, which keeps
    /// <c>ExecuteUpdateAsync</c> out of the picture.
    /// </summary>
    [Test]
    public async Task RefreshAccessToken_WithHashDirectly_Fails()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "hashfail@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1!"),
            DisplayName = "Hash Fail User",
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = true
        };
        databaseContext.Users.Add(user);
        await databaseContext.SaveChangesAsync();

        var service = BuildService(databaseContext);
        var loginResult = await service.LoginWithEmailAsync("hashfail@test.com", "Password1!");
        var rawToken = loginResult.RefreshToken;
        var hash = ComputeTokenHash(rawToken);

        var act = async () => await service.RefreshAccessTokenAsync(hash);
        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "passing the hash as the raw token should not find the stored record (double-hash)");
    }

    [Test]
    public async Task LoginWithGoogle_UnverifiedEmail_ThrowsUnauthorized()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var service = BuildService(databaseContext, StubGoogleValidator("unverified@test.com", isEmailVerified: false));

        var act = async () => await service.LoginWithGoogleAsync("google-id-token");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    /// <summary>
    /// The inverse of what 40.7 asserted here. Google is a registration channel again (40.37), so an
    /// unknown identity is provisioned — but the assertion that matters is the second one: what it
    /// provisions is an account with no membership and therefore no organization claims.
    /// </summary>
    [Test]
    public async Task LoginWithGoogle_UnknownEmail_ProvisionsAnAccountThatBelongsToNoOrganization()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var service = BuildService(databaseContext, StubGoogleValidator("stranger@test.com"));

        var issuedTokenPair = await service.LoginWithGoogleAsync("google-id-token");

        (await databaseContext.Users.CountAsync()).Should().Be(1);
        (await databaseContext.Memberships.CountAsync()).Should()
            .Be(0, "signing up may never attach the account to an organization — only an invite does that");
        issuedTokenPair.OrgId.Should().BeNull();
        issuedTokenPair.OrgRole.Should().BeNull();
    }

    [Test]
    public async Task LoginWithGoogle_UnknownEmail_MarksTheProvisionedAccountVerifiedAndPasswordless()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var service = BuildService(databaseContext, StubGoogleValidator("Stranger@Test.com"));

        await service.LoginWithGoogleAsync("google-id-token");

        var provisioned = await databaseContext.Users.SingleAsync();
        provisioned.Email.Should().Be("stranger@test.com", "addresses are stored normalized");
        provisioned.IsEmailVerified.Should().BeTrue("Google has already proven the address");
        provisioned.PasswordHash.Should().BeNull("there is no password to hash on this path");
        provisioned.GoogleId.Should().Be("google-subject-Stranger@Test.com");
    }

    /// <summary>
    /// The one rejection Google sign-in keeps: an unverified local row holding the same address. It
    /// cannot be signed into (whoever owns it never proved the mailbox) and it cannot be duplicated
    /// either, since <c>Users.Email</c> is unique.
    /// </summary>
    [Test]
    public async Task LoginWithGoogle_AddressHeldByUnverifiedLocalAccount_ThrowsAndCreatesNoSecondUser()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        databaseContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "unverified@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1!"),
            DisplayName = "Unverified",
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = false
        });
        await databaseContext.SaveChangesAsync();
        var service = BuildService(databaseContext, StubGoogleValidator("unverified@test.com"));

        var act = async () => await service.LoginWithGoogleAsync("google-id-token");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await databaseContext.Users.CountAsync()).Should().Be(1);
    }

    /// <summary>
    /// Was a rejection until 40.37. A member-less account now has somewhere to land — the screen
    /// saying an invitation is pending — and refusing it here while the password path admits it was
    /// the asymmetry the platform-staff carve-out used to paper over.
    /// </summary>
    [Test]
    public async Task LoginWithGoogle_ExistingUserWithoutMembership_IssuesTokensWithoutOrgClaims()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        await SeedGoogleUserAsync(databaseContext, "no-membership@test.com");
        var service = BuildService(databaseContext, StubGoogleValidator("no-membership@test.com"));

        var issuedTokenPair = await service.LoginWithGoogleAsync("google-id-token");

        issuedTokenPair.OrgId.Should().BeNull();
        issuedTokenPair.OrgRole.Should().BeNull();
    }

    [Test]
    public async Task LoginWithGoogle_ExistingUserWithDeactivatedMembership_IssuesTokensWithoutOrgClaims()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var user = await SeedGoogleUserAsync(databaseContext, "deactivated@test.com");
        databaseContext.Memberships.Add(new Membership
        {
            UserId = user.Id,
            OrganizationId = Guid.NewGuid(),
            Role = OrgRole.Manager,
            Status = MembershipStatus.Deactivated,
            JoinedAt = DateTime.UtcNow.AddDays(-10),
            DeactivatedAt = DateTime.UtcNow
        });
        await databaseContext.SaveChangesAsync();
        var service = BuildService(databaseContext, StubGoogleValidator("deactivated@test.com"));

        var issuedTokenPair = await service.LoginWithGoogleAsync("google-id-token");

        issuedTokenPair.OrgId.Should().BeNull(
            "a deactivated membership is not an active one, so the offboarded user sees the pending-invitation screen");
        issuedTokenPair.OrgRole.Should().BeNull();
    }

    [Test]
    public async Task RegisterWithEmail_CreatesAnAccountThatBelongsToNoOrganization()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var service = BuildService(databaseContext);

        var registrationResult = await service.RegisterWithEmailAsync(
            "Newcomer@Test.com", "Password1!", "Newcomer");

        var registered = await databaseContext.Users.SingleAsync();
        registered.Email.Should().Be("newcomer@test.com");
        registered.PasswordHash.Should().NotBeNullOrEmpty();
        (await databaseContext.Memberships.CountAsync()).Should().Be(0);
        registrationResult.TokenPair!.OrgId.Should().BeNull();
        registrationResult.TokenPair.IsOnboardingCompleted.Should().BeFalse();
    }

    /// <summary>
    /// The default posture: no code, verified on the spot, signed straight in.
    /// </summary>
    [Test]
    public async Task RegisterWithEmail_WithVerificationDisabled_SignsInWithoutSendingACode()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var emailVerificationService = Substitute.For<IEmailVerificationService>();
        var service = BuildService(
            databaseContext,
            emailVerificationService: emailVerificationService,
            isEmailVerificationEnabled: false);

        var registrationResult = await service.RegisterWithEmailAsync(
            "nocode@test.com", "Password1!", "No Code");

        registrationResult.RequiresEmailVerification.Should().BeFalse();
        registrationResult.TokenPair.Should().NotBeNull();
        (await databaseContext.Users.SingleAsync()).IsEmailVerified.Should().BeTrue();
        await emailVerificationService.DidNotReceiveWithAnyArgs()
            .GenerateAndSendCodeAsync(default!, default!, default);
    }

    [Test]
    public async Task RegisterWithEmail_WithVerificationEnabled_MailsACodeAndIssuesNoSession()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var emailVerificationService = Substitute.For<IEmailVerificationService>();
        var service = BuildService(
            databaseContext,
            emailVerificationService: emailVerificationService,
            isEmailVerificationEnabled: true);

        var registrationResult = await service.RegisterWithEmailAsync(
            "needscode@test.com", "Password1!", "Needs Code");

        registrationResult.RequiresEmailVerification.Should().BeTrue();
        registrationResult.TokenPair.Should().BeNull("no session may exist before the address is proven");
        (await databaseContext.Users.SingleAsync()).IsEmailVerified.Should().BeFalse();
        (await databaseContext.RefreshTokens.CountAsync()).Should().Be(0);
        await emailVerificationService.Received(1)
            .GenerateAndSendCodeAsync("needscode@test.com", "Needs Code", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The flag's other end. One switch has to govern both, or sign-up could demand a code that
    /// login then never asks for.
    /// </summary>
    [Test]
    public async Task LoginWithEmail_WithVerificationEnabled_RefusesAnUnverifiedAccount()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        databaseContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "pending@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1!"),
            DisplayName = "Pending",
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = false
        });
        await databaseContext.SaveChangesAsync();
        var service = BuildService(databaseContext, isEmailVerificationEnabled: true);

        var act = async () => await service.LoginWithEmailAsync("pending@test.com", "Password1!");

        await act.Should().ThrowAsync<EmailNotVerifiedException>(
            "the credential was right — the answer must point at the code, not at the password");
        (await databaseContext.RefreshTokens.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task LoginWithEmail_WithVerificationDisabled_AdmitsAnUnverifiedAccount()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        databaseContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "unproven@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password1!"),
            DisplayName = "Unproven",
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = false
        });
        await databaseContext.SaveChangesAsync();
        var service = BuildService(databaseContext, isEmailVerificationEnabled: false);

        var issuedTokenPair = await service.LoginWithEmailAsync("unproven@test.com", "Password1!");

        issuedTokenPair.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task RegisterWithEmail_DuplicateAddress_IsRejectedRegardlessOfCase()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var service = BuildService(databaseContext);
        await service.RegisterWithEmailAsync("taken@test.com", "Password1!", "First");

        var act = async () => await service.RegisterWithEmailAsync("TAKEN@test.com", "Password1!", "Second");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Email already registered.");
        (await databaseContext.Users.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task RegisterWithEmail_AnnouncesTheNewUserOnce()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var userEventPublisher = Substitute.For<IUserEventPublisher>();
        var service = BuildService(databaseContext, userEventPublisher: userEventPublisher);

        await service.RegisterWithEmailAsync("announced@test.com", "Password1!", "Announced");

        await userEventPublisher.Received(1).PublishRegisteredAsync(
            Arg.Is<UserRegisteredEvent>(published => published.Email == "announced@test.com"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task LoginWithGoogle_ExistingUserWithActiveMembership_IssuesTokensWithOrgClaims()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var user = await SeedGoogleUserAsync(databaseContext, "member@test.com");
        var organizationId = Guid.NewGuid();
        databaseContext.Memberships.Add(new Membership
        {
            UserId = user.Id,
            OrganizationId = organizationId,
            Role = OrgRole.TenancyAdmin,
            Status = MembershipStatus.Active,
            JoinedAt = DateTime.UtcNow
        });
        await databaseContext.SaveChangesAsync();
        var service = BuildService(databaseContext, StubGoogleValidator("member@test.com"));

        var issuedTokenPair = await service.LoginWithGoogleAsync("google-id-token");

        issuedTokenPair.OrgId.Should().Be(organizationId.ToString());
        issuedTokenPair.OrgRole.Should().Be("TenancyAdmin");
    }

    /// <summary>
    /// The behaviour that makes the seam more than a shape: an organization configured for a
    /// method nobody implements is refused, not quietly downgraded to a password. Without this,
    /// switching a customer to SSO would leave their password login silently working alongside it.
    /// </summary>
    [Test]
    public async Task LoginWithEmail_ForOrganizationConfiguredForSso_IsRejectedDespiteCorrectPassword()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
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
        databaseContext.Users.Add(user);
        databaseContext.Memberships.Add(new Membership
        {
            UserId = user.Id,
            OrganizationId = organizationId,
            Role = OrgRole.Manager,
            Status = MembershipStatus.Active,
            JoinedAt = DateTime.UtcNow
        });
        databaseContext.OrganizationAuthConfigurations.Add(new OrganizationAuthConfiguration
        {
            OrganizationId = organizationId,
            Method = AuthMethodNames.Oidc,
            AllowedEmailDomains = ["bigcustomer.test"],
            CreatedAt = DateTime.UtcNow
        });
        await databaseContext.SaveChangesAsync();

        var service = BuildService(databaseContext);

        var act = async () => await service.LoginWithEmailAsync("employee@bigcustomer.test", "Password1!");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await databaseContext.RefreshTokens.CountAsync()).Should().Be(0, "no session may be issued for a refused method");
    }

    [Test]
    public async Task ResolveLoginMethodAsync_ForAnUnknownAddress_StillAnswersPassword()
    {
        await using var databaseContext = InMemoryDbContextFactory.Create();
        var service = BuildService(databaseContext);

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

    private static async Task<User> SeedGoogleUserAsync(IdentityDbContext databaseContext, string email)
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
        databaseContext.Users.Add(user);
        await databaseContext.SaveChangesAsync();
        return user;
    }
}
