using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Profile.Services.Implementation;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Unit;

[TestFixture]
public class ProfileServiceTests
{
    [Test]
    public async Task GetProfileStats_ReturnsIdentityFields_AndZeroedCrossServiceAggregates()
    {
        await using var database = InMemoryDbContextFactory.Create();
        var userId = Guid.NewGuid();
        database.Users.Add(new User { Id = userId, Email = "a@b.com", DisplayName = "Alice" });
        await database.SaveChangesAsync();

        var service = new ProfileService(database, new RecordingUserEventPublisher());
        var stats = await service.GetProfileStatsForUserAsync(userId);

        stats.DisplayName.Should().Be("Alice");
        stats.Email.Should().Be("a@b.com");
        stats.AvatarUrl.Should().Be($"/avatars/{userId}");
        stats.TotalXpAmount.Should().Be(0);
        stats.CurrentStreakDayCount.Should().Be(0);
        stats.CompletedSkillCount.Should().Be(0);
        stats.AverageExerciseScore.Should().Be(0.0);
    }

    [Test]
    public async Task GetProfileStats_Throws_WhenUserMissing()
    {
        await using var database = InMemoryDbContextFactory.Create();
        var service = new ProfileService(database, new RecordingUserEventPublisher());

        var act = async () => await service.GetProfileStatsForUserAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Test]
    public async Task UpdatePersona_CreatesProfileRow_WhenNoneExists()
    {
        await using var database = InMemoryDbContextFactory.Create();
        var service = new ProfileService(database, new RecordingUserEventPublisher());
        var userId = Guid.NewGuid();

        await service.UpdatePersonaForUserAsync(userId, "founder");

        var profile = await database.UserProfiles.SingleAsync(p => p.UserId == userId);
        profile.Persona.Should().Be("founder");
        profile.IsOnboardingCompleted.Should().BeFalse();
    }

    [Test]
    public async Task UpdateProfile_UpdatesDisplayName_UpsertsPersona_AndPublishesUpdatedEvent()
    {
        await using var database = InMemoryDbContextFactory.Create();
        var userId = Guid.NewGuid();
        database.Users.Add(new User { Id = userId, Email = "a@b.com", DisplayName = "Alice" });
        await database.SaveChangesAsync();

        var publisher = new RecordingUserEventPublisher();
        var service = new ProfileService(database, publisher);

        await service.UpdateProfileForUserAsync(userId, "Alice Smith", "account_executive");

        var user = await database.Users.SingleAsync(u => u.Id == userId);
        user.DisplayName.Should().Be("Alice Smith");

        var profile = await database.UserProfiles.SingleAsync(p => p.UserId == userId);
        profile.Persona.Should().Be("account_executive");

        publisher.Updated.Should().ContainSingle()
            .Which.DisplayName.Should().Be("Alice Smith");
    }

    [Test]
    public async Task UpdateProfile_LeavesPersonaUntouched_WhenNotProvided()
    {
        await using var database = InMemoryDbContextFactory.Create();
        var userId = Guid.NewGuid();
        database.Users.Add(new User { Id = userId, Email = "a@b.com", DisplayName = "Alice" });
        await database.SaveChangesAsync();

        var service = new ProfileService(database, new RecordingUserEventPublisher());

        await service.UpdateProfileForUserAsync(userId, "Alice Smith", null);

        var user = await database.Users.SingleAsync(u => u.Id == userId);
        user.DisplayName.Should().Be("Alice Smith");
        (await database.UserProfiles.AnyAsync(p => p.UserId == userId)).Should().BeFalse();
    }

    [Test]
    public async Task UpdateProfile_Throws_WhenUserMissing()
    {
        await using var database = InMemoryDbContextFactory.Create();
        var service = new ProfileService(database, new RecordingUserEventPublisher());

        var act = async () => await service.UpdateProfileForUserAsync(Guid.NewGuid(), "Bob", null);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
