using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Eventing;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Features.Gamification.Services.Implementation;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Tests.Unit;

[TestFixture]
public sealed class StreakServiceTests
{
    private GamificationDbContext _databaseContext = null!;
    private IGamificationEventPublisher _eventPublisher = null!;
    private StreakService _streakService = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseContext = GamificationDbContextFactory.CreateInMemory();
        _eventPublisher = Substitute.For<IGamificationEventPublisher>();
        var settingsService = new GamificationSettingsService(_databaseContext);
        var grantService = new ExperiencePointsGrantService(
            _databaseContext, _eventPublisher, NullLogger<ExperiencePointsGrantService>.Instance);
        _streakService = new StreakService(_databaseContext, settingsService, grantService, _eventPublisher);
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    [Test]
    public async Task RegisterActivityAsync_WhenNoStreak_CreatesStreakWithDayOne()
    {
        var userId = Guid.NewGuid();

        await _streakService.RegisterActivityAsync(userId);

        var streak = await _databaseContext.UserStreaks.FirstAsync(record => record.UserId == userId);
        streak.CurrentStreakDayCount.Should().Be(1);
        streak.LongestStreakDayCount.Should().Be(1);
    }

    [Test]
    public async Task RegisterActivityAsync_SameDayTwice_DoesNotIncrement()
    {
        var userId = Guid.NewGuid();

        await _streakService.RegisterActivityAsync(userId);
        await _streakService.RegisterActivityAsync(userId);

        var streak = await _databaseContext.UserStreaks.FirstAsync(record => record.UserId == userId);
        streak.CurrentStreakDayCount.Should().Be(1);
    }

    [Test]
    public async Task RegisterActivityAsync_AfterGap_ResetsToOne()
    {
        var userId = Guid.NewGuid();
        _databaseContext.UserStreaks.Add(new UserStreak
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentStreakDayCount = 5,
            LongestStreakDayCount = 5,
            LastActivityDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3),
        });
        await _databaseContext.SaveChangesAsync();

        await _streakService.RegisterActivityAsync(userId);

        var streak = await _databaseContext.UserStreaks.FirstAsync(record => record.UserId == userId);
        streak.CurrentStreakDayCount.Should().Be(1);
        streak.LongestStreakDayCount.Should().Be(5);
    }

    [Test]
    public async Task RegisterActivityAsync_ConsecutiveDay_IncrementsStreak()
    {
        var userId = Guid.NewGuid();
        _databaseContext.UserStreaks.Add(new UserStreak
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentStreakDayCount = 6,
            LongestStreakDayCount = 6,
            LastActivityDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
        });
        _databaseContext.StreakMilestones.Add(new StreakMilestone { DayCount = 7, XpReward = 50 });
        await _databaseContext.SaveChangesAsync();

        await _streakService.RegisterActivityAsync(userId);

        var streak = await _databaseContext.UserStreaks.FirstAsync(record => record.UserId == userId);
        streak.CurrentStreakDayCount.Should().Be(7);

        var bonus = await _databaseContext.UserExperiencePointsRecords
            .Where(record => record.UserId == userId && record.Source == ExperiencePointsSources.StreakBonus)
            .SumAsync(record => record.Amount);
        bonus.Should().Be(50);

        await _eventPublisher.Received().PublishStreakMilestoneAsync(
            Arg.Is<StreakMilestoneEvent>(payload => payload.UserId == userId && payload.DayCount == 7 && payload.BonusXp == 50),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RegisterActivityAsync_WhenNoMilestoneReached_DoesNotEmitMilestone()
    {
        var userId = Guid.NewGuid();
        _databaseContext.UserStreaks.Add(new UserStreak
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentStreakDayCount = 2,
            LongestStreakDayCount = 2,
            LastActivityDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
        });
        _databaseContext.StreakMilestones.Add(new StreakMilestone { DayCount = 7, XpReward = 50 });
        await _databaseContext.SaveChangesAsync();

        await _streakService.RegisterActivityAsync(userId);

        await _eventPublisher.DidNotReceive().PublishStreakMilestoneAsync(
            Arg.Any<StreakMilestoneEvent>(), Arg.Any<CancellationToken>());
    }
}
