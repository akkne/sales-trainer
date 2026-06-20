using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Avatars.Services.Implementation;
using Sellevate.Identity.Infrastructure.Storage.Abstract;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Unit;

[TestFixture]
public class AvatarServiceTests
{
    [Test]
    public async Task UploadAvatar_MarksUploaded_AndEmitsAvatarChanged()
    {
        await using var database = InMemoryDbContextFactory.Create();
        var userId = Guid.NewGuid();
        database.Users.Add(new User { Id = userId, Email = "a@b.com", DisplayName = "A" });
        await database.SaveChangesAsync();

        var storage = Substitute.For<IObjectStorage>();
        var events = new RecordingUserEventPublisher();
        var service = new AvatarService(database, storage, events, NullLogger<AvatarService>.Instance);

        using var content = new MemoryStream([1, 2, 3]);
        var key = await service.UploadAvatarAsync(userId, content, "pic.png");

        key.Should().Be($"users/{userId}/avatar.png");
        var user = await database.Users.SingleAsync(u => u.Id == userId);
        user.AvatarType.Should().Be(AvatarKind.Uploaded);
        user.AvatarKey.Should().Be(key);

        events.AvatarChanged.Should().ContainSingle();
        events.AvatarChanged.Single().AvatarKey.Should().Be(key);
        await storage.Received(1).PutAsync(key, Arg.Any<Stream>(), "image/png", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ResetToDefault_ClearsKey_AndEmitsAvatarChangedWithNullKey()
    {
        await using var database = InMemoryDbContextFactory.Create();
        var userId = Guid.NewGuid();
        database.Users.Add(new User
        {
            Id = userId,
            Email = "a@b.com",
            DisplayName = "A",
            AvatarType = AvatarKind.Uploaded,
            AvatarKey = $"users/{userId}/avatar.png"
        });
        await database.SaveChangesAsync();

        var storage = Substitute.For<IObjectStorage>();
        var events = new RecordingUserEventPublisher();
        var service = new AvatarService(database, storage, events, NullLogger<AvatarService>.Instance);

        await service.ResetToDefaultAsync(userId);

        var user = await database.Users.SingleAsync(u => u.Id == userId);
        user.AvatarType.Should().Be(AvatarKind.Default);
        user.AvatarKey.Should().BeNull();

        events.AvatarChanged.Should().ContainSingle();
        events.AvatarChanged.Single().AvatarKey.Should().BeNull();
    }
}
