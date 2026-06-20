using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Eventing;
using Sellevate.Gamification.Features.Achievements.Models;
using Sellevate.Gamification.Features.Achievements.Services.Implementation;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Tests.Unit;

[TestFixture]
public sealed class AchievementServiceTests
{
    private GamificationDbContext _databaseContext = null!;
    private IGamificationEventPublisher _eventPublisher = null!;
    private AchievementService _achievementService = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseContext = GamificationDbContextFactory.CreateInMemory();
        _eventPublisher = Substitute.For<IGamificationEventPublisher>();
        _achievementService = new AchievementService(_databaseContext, _eventPublisher);
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    [Test]
    public async Task EvaluateAchievementsAsync_UnlocksExperiencePointsThresholdAchievement()
    {
        var userId = Guid.NewGuid();
        _databaseContext.Achievements.Add(new Achievement
        {
            Id = Guid.NewGuid(),
            Key = "xp_100",
            Title = "First 100 XP",
            ConditionType = AchievementConditionTypes.ExperiencePointsTotal,
            ConditionThreshold = 100,
        });
        _databaseContext.UserExperiencePointsRecords.Add(new UserExperiencePointsRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = 120,
            Source = ExperiencePointsSources.Exercise,
            EarnedAt = DateTime.UtcNow,
        });
        await _databaseContext.SaveChangesAsync();

        var unlocked = await _achievementService.EvaluateAchievementsAsync(userId);

        unlocked.Should().ContainSingle().Which.Should().Be("xp_100");
        await _eventPublisher.Received(1).PublishAchievementUnlockedAsync(
            Arg.Any<AchievementUnlockedEvent>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EvaluateAchievementsAsync_DoesNotUnlockBelowThreshold()
    {
        var userId = Guid.NewGuid();
        _databaseContext.Achievements.Add(new Achievement
        {
            Id = Guid.NewGuid(),
            Key = "xp_500",
            Title = "500 XP",
            ConditionType = AchievementConditionTypes.ExperiencePointsTotal,
            ConditionThreshold = 500,
        });
        _databaseContext.UserExperiencePointsRecords.Add(new UserExperiencePointsRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = 100,
            Source = ExperiencePointsSources.Exercise,
            EarnedAt = DateTime.UtcNow,
        });
        await _databaseContext.SaveChangesAsync();

        var unlocked = await _achievementService.EvaluateAchievementsAsync(userId);

        unlocked.Should().BeEmpty();
    }

    [Test]
    public async Task EvaluateAchievementsAsync_IsIdempotent_DoesNotUnlockTwice()
    {
        var userId = Guid.NewGuid();
        _databaseContext.Achievements.Add(new Achievement
        {
            Id = Guid.NewGuid(),
            Key = "streak_7",
            Title = "Week of fire",
            ConditionType = AchievementConditionTypes.StreakDays,
            ConditionThreshold = 7,
        });
        _databaseContext.UserStreaks.Add(new UserStreak
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentStreakDayCount = 8,
            LongestStreakDayCount = 8,
            LastActivityDate = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        await _databaseContext.SaveChangesAsync();

        var firstPass = await _achievementService.EvaluateAchievementsAsync(userId);
        var secondPass = await _achievementService.EvaluateAchievementsAsync(userId);

        firstPass.Should().ContainSingle();
        secondPass.Should().BeEmpty();
        var totalUnlocked = await _databaseContext.UserAchievements.CountAsync(record => record.UserId == userId);
        totalUnlocked.Should().Be(1);
    }

    [Test]
    public async Task GetAchievementsForUserAsync_ReturnsUnlockedFlagPerAchievement()
    {
        var userId = Guid.NewGuid();
        var unlockedId = Guid.NewGuid();
        _databaseContext.Achievements.AddRange(
            new Achievement { Id = unlockedId, Key = "a", Title = "A", ConditionType = AchievementConditionTypes.FirstLesson, SortOrder = 1 },
            new Achievement { Id = Guid.NewGuid(), Key = "b", Title = "B", ConditionType = AchievementConditionTypes.LessonCount, ConditionThreshold = 5, SortOrder = 2 });
        _databaseContext.UserAchievements.Add(new UserAchievement
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AchievementId = unlockedId,
            UnlockedAt = DateTime.UtcNow,
        });
        await _databaseContext.SaveChangesAsync();

        var achievements = await _achievementService.GetAchievementsForUserAsync(userId);

        achievements.Should().HaveCount(2);
        achievements.Single(achievement => achievement.Key == "a").IsUnlocked.Should().BeTrue();
        achievements.Single(achievement => achievement.Key == "b").IsUnlocked.Should().BeFalse();
    }
}
