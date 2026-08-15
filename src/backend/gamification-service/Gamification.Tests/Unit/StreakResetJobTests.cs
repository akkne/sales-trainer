using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Sellevate.Gamification.Features.Gamification;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Tests.Unit;

[TestFixture]
public sealed class StreakResetJobTests
{
    private GamificationDbContext _databaseContext = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseContext = GamificationDbContextFactory.CreateInMemory();
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    [Test]
    public async Task ExecuteAsync_ResetsStaleStreaksButKeepsRecentOnes()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var staleUserId = Guid.NewGuid();
        var activeUserId = Guid.NewGuid();
        var yesterdayUserId = Guid.NewGuid();

        _databaseContext.UserStreaks.AddRange(
            new UserStreak { Id = Guid.NewGuid(), UserId = staleUserId, CurrentStreakDayCount = 9, LongestStreakDayCount = 9, LastActivityDate = today.AddDays(-3) },
            new UserStreak { Id = Guid.NewGuid(), UserId = activeUserId, CurrentStreakDayCount = 4, LongestStreakDayCount = 4, LastActivityDate = today },
            new UserStreak { Id = Guid.NewGuid(), UserId = yesterdayUserId, CurrentStreakDayCount = 3, LongestStreakDayCount = 3, LastActivityDate = today.AddDays(-1) });
        await _databaseContext.SaveChangesAsync();

        // Phase 40.13: StreakResetJob.ExecuteAsync is now the per-organization loop and needs a
        // service provider; the reset rule itself moved to this method, which takes the scoped
        // context the loop would have handed it.
        await StreakResetJob.ResetStaleStreaksAsync(
            _databaseContext, new FixedStreakClock(), CancellationToken.None);

        var staleStreak = await _databaseContext.UserStreaks.FirstAsync(record => record.UserId == staleUserId);
        var activeStreak = await _databaseContext.UserStreaks.FirstAsync(record => record.UserId == activeUserId);
        var yesterdayStreak = await _databaseContext.UserStreaks.FirstAsync(record => record.UserId == yesterdayUserId);

        staleStreak.CurrentStreakDayCount.Should().Be(0);
        activeStreak.CurrentStreakDayCount.Should().Be(4);
        yesterdayStreak.CurrentStreakDayCount.Should().Be(3);
        staleStreak.LongestStreakDayCount.Should().Be(9);
    }
}
