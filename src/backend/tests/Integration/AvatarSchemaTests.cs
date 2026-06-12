using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.Avatars.Models;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Integration;

[TestFixture]
public class AvatarSchemaTests
{
    private AppDbContext _db = null!;
    private IServiceScope _scope = null!;

    [SetUp]
    public void SetUp()
    {
        _scope = IntegrationTestSetup.Factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    [TearDown]
    public void TearDown()
    {
        _scope.Dispose();
    }

    [Test]
    public async Task User_AvatarFields_RoundTrip()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"avatar_{Guid.NewGuid()}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            DisplayName = "Avatar User",
            CreatedAt = DateTime.UtcNow,
            Role = UserRole.User,
            AvatarType = AvatarKind.Uploaded,
            AvatarKey = "uploads/user-abc123.png",
            DefaultAvatarIndex = 3
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var loaded = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);

        loaded.AvatarType.Should().Be(AvatarKind.Uploaded);
        loaded.AvatarKey.Should().Be("uploads/user-abc123.png");
        loaded.DefaultAvatarIndex.Should().Be(3);
    }

    [Test]
    public async Task User_DefaultAvatarFields_DefaultToZeroAndNull()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"noavatar_{Guid.NewGuid()}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            DisplayName = "No Avatar User",
            CreatedAt = DateTime.UtcNow,
            Role = UserRole.User
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var loaded = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);

        loaded.AvatarType.Should().Be(AvatarKind.Default);
        loaded.AvatarKey.Should().BeNull();
        loaded.DefaultAvatarIndex.Should().Be(0);
    }

    [Test]
    public async Task DefaultAvatar_RoundTrip()
    {
        var avatar = new DefaultAvatar
        {
            Id = Guid.NewGuid(),
            Index = 100,
            ObjectKey = "defaults/avatar-100.png",
            CreatedAt = DateTime.UtcNow
        };
        _db.DefaultAvatars.Add(avatar);
        await _db.SaveChangesAsync();

        var loaded = await _db.DefaultAvatars.AsNoTracking().SingleAsync(a => a.Id == avatar.Id);

        loaded.Index.Should().Be(100);
        loaded.ObjectKey.Should().Be("defaults/avatar-100.png");
    }

    [Test]
    public async Task DefaultAvatar_DuplicateIndex_ThrowsUniqueConstraintViolation()
    {
        var index = 200 + new Random().Next(0, 1000);

        _db.DefaultAvatars.Add(new DefaultAvatar
        {
            Id = Guid.NewGuid(),
            Index = index,
            ObjectKey = "defaults/avatar-first.png",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        _db.DefaultAvatars.Add(new DefaultAvatar
        {
            Id = Guid.NewGuid(),
            Index = index,
            ObjectKey = "defaults/avatar-second.png",
            CreatedAt = DateTime.UtcNow
        });

        var act = async () => await _db.SaveChangesAsync();
        // Npgsql wraps the Postgres 23505 violation as DbUpdateException
        await act.Should().ThrowAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(
            because: "PostgreSQL enforces the unique index on DefaultAvatar.Index");
    }
}
