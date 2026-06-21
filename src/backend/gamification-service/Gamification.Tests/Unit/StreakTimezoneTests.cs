using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Gamification.Eventing;
using Sellevate.Gamification.Features.Gamification;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Features.Gamification.Services.Implementation;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Tests.Unit;

/// <summary>
/// GA3: Streak day boundary must be computed consistently in the configured timezone.
/// These tests pin the clock to an explicit date so we can verify day-boundary logic
/// independently of the host OS timezone.
/// </summary>
[TestFixture]
public sealed class StreakTimezoneTests
{
    private GamificationDbContext _databaseContext = null!;
    private IGamificationEventPublisher _eventPublisher = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseContext = GamificationDbContextFactory.CreateInMemory();
        _eventPublisher = Substitute.For<IGamificationEventPublisher>();
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    private StreakService CreateService(DateOnly today)
    {
        var settingsService = new GamificationSettingsService(_databaseContext);
        var grantService = new ExperiencePointsGrantService(
            _databaseContext, _eventPublisher, NullLogger<ExperiencePointsGrantService>.Instance);
        return new StreakService(_databaseContext, settingsService, grantService, _eventPublisher, new FixedStreakClock(today));
    }

    [Test]
    public async Task RegisterActivity_SameDayInConfiguredTimezone_DoesNotIncrementStreak()
    {
        // Arrange: first call on 2026-06-15 creates the streak
        var dayOne = new DateOnly(2026, 6, 15);
        var service = CreateService(dayOne);
        var userId = Guid.NewGuid();

        await service.RegisterActivityAsync(userId);

        // Act: second call on the same day (same fixed clock) — should be idempotent
        await service.RegisterActivityAsync(userId);

        // Assert
        var streak = await _databaseContext.UserStreaks.FirstAsync(s => s.UserId == userId);
        streak.CurrentStreakDayCount.Should().Be(1);
    }

    [Test]
    public async Task RegisterActivity_NextDayInConfiguredTimezone_IncrementsStreak()
    {
        // Arrange: create streak on day 1
        var dayOne = new DateOnly(2026, 6, 15);
        var serviceDay1 = CreateService(dayOne);
        var userId = Guid.NewGuid();
        await serviceDay1.RegisterActivityAsync(userId);

        // Act: next calendar day in the configured timezone
        var dayTwo = dayOne.AddDays(1);
        var serviceDay2 = CreateService(dayTwo);
        await serviceDay2.RegisterActivityAsync(userId);

        // Assert
        var streak = await _databaseContext.UserStreaks.FirstAsync(s => s.UserId == userId);
        streak.CurrentStreakDayCount.Should().Be(2);
    }

    [Test]
    public async Task RegisterActivity_AfterGapInConfiguredTimezone_ResetsToOne()
    {
        // Arrange: create streak with LastActivityDate two days ago
        var twoDaysAgo = new DateOnly(2026, 6, 13);
        var today = new DateOnly(2026, 6, 15);
        var userId = Guid.NewGuid();
        _databaseContext.UserStreaks.Add(new UserStreak
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentStreakDayCount = 10,
            LongestStreakDayCount = 10,
            LastActivityDate = twoDaysAgo,
        });
        await _databaseContext.SaveChangesAsync();

        // Act
        var service = CreateService(today);
        await service.RegisterActivityAsync(userId);

        // Assert: gap resets streak
        var streak = await _databaseContext.UserStreaks.FirstAsync(s => s.UserId == userId);
        streak.CurrentStreakDayCount.Should().Be(1);
        streak.LongestStreakDayCount.Should().Be(10); // longest preserved
    }

    [Test]
    public async Task StreakResetJob_UsesConfiguredTimezone_ResetsOnlyStaleStreaks()
    {
        // Arrange: three streaks with activities at different dates relative to "today"
        var today = new DateOnly(2026, 6, 15);
        var yesterday = today.AddDays(-1);
        var twoDaysAgo = today.AddDays(-2);

        var staleUserId = Guid.NewGuid();
        var yesterdayUserId = Guid.NewGuid();
        var todayUserId = Guid.NewGuid();

        _databaseContext.UserStreaks.AddRange(
            new UserStreak { Id = Guid.NewGuid(), UserId = staleUserId, CurrentStreakDayCount = 5, LongestStreakDayCount = 5, LastActivityDate = twoDaysAgo },
            new UserStreak { Id = Guid.NewGuid(), UserId = yesterdayUserId, CurrentStreakDayCount = 3, LongestStreakDayCount = 3, LastActivityDate = yesterday },
            new UserStreak { Id = Guid.NewGuid(), UserId = todayUserId, CurrentStreakDayCount = 1, LongestStreakDayCount = 1, LastActivityDate = today });
        await _databaseContext.SaveChangesAsync();

        var job = new StreakResetJob(_databaseContext, new FixedStreakClock(today), NullLogger<StreakResetJob>.Instance);

        // Act
        await job.ExecuteAsync();

        // Assert
        var staleStreak = await _databaseContext.UserStreaks.FirstAsync(s => s.UserId == staleUserId);
        var yesterdayStreak = await _databaseContext.UserStreaks.FirstAsync(s => s.UserId == yesterdayUserId);
        var todayStreak = await _databaseContext.UserStreaks.FirstAsync(s => s.UserId == todayUserId);

        staleStreak.CurrentStreakDayCount.Should().Be(0, "two-days-ago activity is stale");
        yesterdayStreak.CurrentStreakDayCount.Should().Be(3, "yesterday activity is still valid");
        todayStreak.CurrentStreakDayCount.Should().Be(1, "today activity is still valid");
    }
}
