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

        var service = new ProfileService(database);
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
        var service = new ProfileService(database);

        var act = async () => await service.GetProfileStatsForUserAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Test]
    public async Task UpdatePersona_CreatesProfileRow_WhenNoneExists()
    {
        await using var database = InMemoryDbContextFactory.Create();
        var service = new ProfileService(database);
        var userId = Guid.NewGuid();

        await service.UpdatePersonaForUserAsync(userId, "founder");

        var profile = await database.UserProfiles.SingleAsync(p => p.UserId == userId);
        profile.Persona.Should().Be("founder");
        profile.IsOnboardingCompleted.Should().BeFalse();
    }
}
