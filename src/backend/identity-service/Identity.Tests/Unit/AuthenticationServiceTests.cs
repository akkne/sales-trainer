using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Auth.Services.Abstract;
using Sellevate.Identity.Features.Auth.Services.Implementation;
using Sellevate.Identity.Infrastructure.Configuration;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Unit;

[TestFixture]
public class AuthenticationServiceTests
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

    private static readonly IOptions<GoogleAuthConfiguration> GoogleOptions =
        Options.Create(new GoogleAuthConfiguration { ClientId = "test" });

    private static AuthenticationService BuildService(IdentityDbContext db) =>
        new(
            db,
            Substitute.For<IEmailVerificationService>(),
            new RecordingUserEventPublisher(),
            JwtOptions,
            GoogleOptions,
            NullLogger<AuthenticationService>.Instance);

    // ── pre-check path ────────────────────────────────────────────────────────

    [Test]
    public async Task RegisterWithEmail_DuplicateEmail_PreCheck_ThrowsInvalidOperationException()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "dup@test.com",
            DisplayName = "Existing",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = BuildService(db);

        var act = async () => await service.RegisterWithEmailAsync("dup@test.com", "Password1!", "New User");

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Email already registered.");
    }

    [Test]
    public async Task RegisterWithEmail_DuplicateEmail_IsCaseInsensitive()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "dup@test.com",
            DisplayName = "Existing",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = BuildService(db);

        var act = async () => await service.RegisterWithEmailAsync("DUP@TEST.COM", "Password1!", "New User");

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Email already registered.");
    }

    // ── DbUpdateException race path ───────────────────────────────────────────

    [Test]
    public async Task RegisterWithEmail_DbUpdateExceptionOnSave_ThrowsInvalidOperationExceptionWithSameMessage()
    {
        await using var db = new ThrowingOnSaveDbContext();
        var service = BuildService(db);

        var act = async () => await service.RegisterWithEmailAsync("race@test.com", "Password1!", "Racer");

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Email already registered.");
    }

    /// <summary>
    /// In-memory DbContext whose SaveChangesAsync always throws DbUpdateException,
    /// simulating the unique-index violation that can occur in a concurrent registration race.
    /// </summary>
    private sealed class ThrowingOnSaveDbContext : IdentityDbContext
    {
        public ThrowingOnSaveDbContext()
            : base(new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options)
        { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new DbUpdateException("Unique constraint violation (simulated)");
    }
}
