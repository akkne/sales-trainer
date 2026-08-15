using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Sellevate.Identity.Features.Auth.Constants;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Auth.Services.Implementation;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Unit;

/// <summary>
/// Phase 40.8 — the single <c>IAuthProvider</c> implementation. These assertions used to live
/// inside <c>AuthenticationServiceSecurityTests</c>; they belong here now that the bcrypt check
/// moved behind the provider seam.
/// </summary>
[TestFixture]
public class PasswordAuthProviderTests
{
    private const string CorrectPassword = "Password123!";

    [Test]
    public void Method_IsThePasswordWireName()
    {
        using var databaseContext = InMemoryDbContextFactory.Create();
        var provider = CreateProvider(databaseContext);

        provider.Method.Should().Be(AuthMethodNames.Password);
    }

    [Test]
    public async Task AuthenticateAsync_WithCorrectPassword_ReturnsTheUser()
    {
        using var databaseContext = InMemoryDbContextFactory.Create();
        var user = await SeedUserAsync(databaseContext, "member@test.com", CorrectPassword);
        var provider = CreateProvider(databaseContext);

        var result = await provider.AuthenticateAsync(new AuthRequest("member@test.com", CorrectPassword));

        result.IsAuthenticated.Should().BeTrue();
        result.AuthenticatedUser!.Id.Should().Be(user.Id);
    }

    [Test]
    public async Task AuthenticateAsync_WithWrongPassword_Fails()
    {
        using var databaseContext = InMemoryDbContextFactory.Create();
        await SeedUserAsync(databaseContext, "member@test.com", CorrectPassword);
        var provider = CreateProvider(databaseContext);

        var result = await provider.AuthenticateAsync(new AuthRequest("member@test.com", "WrongPassword1!"));

        result.IsAuthenticated.Should().BeFalse();
        result.AuthenticatedUser.Should().BeNull();
    }

    [Test]
    public async Task AuthenticateAsync_ForUnknownEmail_Fails()
    {
        using var databaseContext = InMemoryDbContextFactory.Create();
        var provider = CreateProvider(databaseContext);

        var result = await provider.AuthenticateAsync(new AuthRequest("nobody@test.com", CorrectPassword));

        result.IsAuthenticated.Should().BeFalse();
    }

    /// <summary>
    /// A Google-only account has no password hash. Passing an empty password must not reach
    /// BCrypt.Verify with a null hash, and must never succeed.
    /// </summary>
    [Test]
    public async Task AuthenticateAsync_ForAccountWithoutPasswordHash_Fails()
    {
        using var databaseContext = InMemoryDbContextFactory.Create();
        databaseContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "google-only@test.com",
            PasswordHash = null,
            GoogleId = "google-subject",
            DisplayName = "Google Only",
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = true
        });
        await databaseContext.SaveChangesAsync();
        var provider = CreateProvider(databaseContext);

        var result = await provider.AuthenticateAsync(new AuthRequest("google-only@test.com", string.Empty));

        result.IsAuthenticated.Should().BeFalse();
    }

    private static PasswordAuthProvider CreateProvider(IdentityDbContext databaseContext)
        => new(databaseContext, NullLogger<PasswordAuthProvider>.Instance);

    private static async Task<User> SeedUserAsync(IdentityDbContext databaseContext, string email, string password)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            DisplayName = "Member",
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = true
        };

        databaseContext.Users.Add(user);
        await databaseContext.SaveChangesAsync();
        return user;
    }
}
