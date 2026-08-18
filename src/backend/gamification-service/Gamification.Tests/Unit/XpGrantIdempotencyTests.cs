using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Eventing;
using Sellevate.Gamification.Features.Achievements.Services.Implementation;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Features.Gamification.Services.Implementation;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Tests.Unit;

/// <summary>
/// Proves that a second handler invocation with the SAME source event id
/// does NOT double-grant XP and does NOT double-apply the streak bonus.
/// The in-memory provider does not enforce the filtered unique index,
/// so these tests exercise the application-level guard (existing-record check).
/// </summary>
[TestFixture]
public sealed class XpGrantIdempotencyTests
{
    private GamificationDbContext _databaseContext = null!;
    private IGamificationEventPublisher _eventPublisher = null!;
    private ExperiencePointsGrantService _grantService = null!;
    private GamificationEventHandler _eventHandler = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseContext = GamificationDbContextFactory.CreateInMemory();
        _eventPublisher = Substitute.For<IGamificationEventPublisher>();

        var settingsService = new GamificationSettingsService(_databaseContext);
        _grantService = new ExperiencePointsGrantService(
            _databaseContext, _eventPublisher, NullLogger<ExperiencePointsGrantService>.Instance);
        var streakService = new StreakService(_databaseContext, settingsService, _grantService, _eventPublisher, new FixedStreakClock());
        var achievementService = new AchievementService(_databaseContext, _eventPublisher);
        var learningProgressService = new LearningProgressService(_databaseContext);

        _eventHandler = new GamificationEventHandler(
            _grantService, settingsService, streakService, achievementService, learningProgressService);
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    [Test]
    public async Task GrantAsync_SameSourceEventId_SecondCallIsNoOp()
    {
        var userId = Guid.NewGuid();
        var sourceEventId = Guid.NewGuid();

        await _grantService.GrantAsync(userId, 50, ExperiencePointsSources.Exercise, sourceEventId: sourceEventId);
        await _grantService.GrantAsync(userId, 50, ExperiencePointsSources.Exercise, sourceEventId: sourceEventId);

        var totalXp = await _databaseContext.UserExperiencePointsRecords
            .Where(r => r.UserId == userId)
            .SumAsync(r => r.Amount);

        totalXp.Should().Be(50, "the second call with the same sourceEventId must be skipped");
    }

    [Test]
    public async Task GrantAsync_DifferentSourceEventIds_BothGrantsApplied()
    {
        var userId = Guid.NewGuid();

        await _grantService.GrantAsync(userId, 30, ExperiencePointsSources.Exercise, sourceEventId: Guid.NewGuid());
        await _grantService.GrantAsync(userId, 30, ExperiencePointsSources.Exercise, sourceEventId: Guid.NewGuid());

        var totalXp = await _databaseContext.UserExperiencePointsRecords
            .Where(r => r.UserId == userId)
            .SumAsync(r => r.Amount);

        totalXp.Should().Be(60, "two different event ids must both be granted");
    }

    [Test]
    public async Task GrantAsync_NullSourceEventId_AlwaysGrants()
    {
        var userId = Guid.NewGuid();

        await _grantService.GrantAsync(userId, 10, ExperiencePointsSources.Exercise, sourceEventId: null);
        await _grantService.GrantAsync(userId, 10, ExperiencePointsSources.Exercise, sourceEventId: null);

        var totalXp = await _databaseContext.UserExperiencePointsRecords
            .Where(r => r.UserId == userId)
            .SumAsync(r => r.Amount);

        totalXp.Should().Be(20, "null sourceEventId grants are not deduplicated");
    }

    [Test]
    public async Task HandleExerciseCompletedAsync_SameSourceEventId_DoesNotDoubleGrantXp()
    {
        var userId = Guid.NewGuid();
        var sourceEventId = Guid.NewGuid();

        await _eventHandler.HandleExerciseCompletedAsync(
            userId, "choose_option", isCorrect: true, sourceEventId: sourceEventId);

        await _eventHandler.HandleExerciseCompletedAsync(
            userId, "choose_option", isCorrect: true, sourceEventId: sourceEventId);

        var exerciseXp = await _databaseContext.UserExperiencePointsRecords
            .Where(r => r.UserId == userId && r.Source == ExperiencePointsSources.Exercise)
            .SumAsync(r => r.Amount);

        exerciseXp.Should().Be(10, "redelivery with the same sourceEventId must not double-grant exercise XP");
    }

    /// <summary>
    /// The streak is seeded at day six so the first invocation crosses the seven-day milestone. On the
    /// redelivery the idempotency guard fires before the streak logic runs — and the streak would not
    /// have advanced anyway, because its last activity is already today, so the bonus cannot be paid
    /// twice by either route.
    /// </summary>
    [Test]
    public async Task HandleExerciseCompletedAsync_SameSourceEventId_DoesNotDoubleGrantStreakBonus()
    {
        var userId = Guid.NewGuid();
        var sourceEventId = Guid.NewGuid();

        _databaseContext.UserStreaks.Add(new UserStreak
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentStreakDayCount = 6,
            LongestStreakDayCount = 6,
            LastActivityDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
        });
        _databaseContext.StreakMilestones.Add(new StreakMilestone { DayCount = 7, XpReward = 100 });
        await _databaseContext.SaveChangesAsync();

        await _eventHandler.HandleExerciseCompletedAsync(
            userId, "choose_option", isCorrect: true, sourceEventId: sourceEventId);

        var bonusAfterFirst = await _databaseContext.UserExperiencePointsRecords
            .Where(r => r.UserId == userId && r.Source == ExperiencePointsSources.StreakBonus)
            .SumAsync(r => r.Amount);

        bonusAfterFirst.Should().Be(100);

        await _eventHandler.HandleExerciseCompletedAsync(
            userId, "choose_option", isCorrect: true, sourceEventId: sourceEventId);

        var bonusAfterSecond = await _databaseContext.UserExperiencePointsRecords
            .Where(r => r.UserId == userId && r.Source == ExperiencePointsSources.StreakBonus)
            .SumAsync(r => r.Amount);

        bonusAfterSecond.Should().Be(100, "streak bonus must not be doubled on redelivery");
    }

    [Test]
    public async Task HandleDialogEvaluatedAsync_SameSourceEventId_DoesNotDoubleGrantXp()
    {
        var userId = Guid.NewGuid();
        var sourceEventId = Guid.NewGuid();

        await _eventHandler.HandleDialogEvaluatedAsync(userId, experiencePointsEarned: 75, sourceEventId: sourceEventId);
        await _eventHandler.HandleDialogEvaluatedAsync(userId, experiencePointsEarned: 75, sourceEventId: sourceEventId);

        var dialogXp = await _databaseContext.UserExperiencePointsRecords
            .Where(r => r.UserId == userId && r.Source == ExperiencePointsSources.Dialog)
            .SumAsync(r => r.Amount);

        dialogXp.Should().Be(75, "redelivery with the same sourceEventId must not double-grant dialog XP");
    }
}
