using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Eventing;
using Sellevate.Gamification.Features.Achievements.Models;
using Sellevate.Gamification.Features.Achievements.Services.Implementation;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Features.Gamification.Services.Implementation;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Tests.Unit;

[TestFixture]
public sealed class GamificationEventHandlerTests
{
    private GamificationDbContext _databaseContext = null!;
    private IGamificationEventPublisher _eventPublisher = null!;
    private GamificationEventHandler _eventHandler = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseContext = GamificationDbContextFactory.CreateInMemory();
        _eventPublisher = Substitute.For<IGamificationEventPublisher>();

        var settingsService = new GamificationSettingsService(_databaseContext);
        var grantService = new ExperiencePointsGrantService(
            _databaseContext, _eventPublisher, NullLogger<ExperiencePointsGrantService>.Instance);
        var streakService = new StreakService(_databaseContext, settingsService, grantService, _eventPublisher, new FixedStreakClock());
        var achievementService = new AchievementService(_databaseContext, _eventPublisher);
        var learningProgressService = new LearningProgressService(_databaseContext);

        _eventHandler = new GamificationEventHandler(
            grantService, settingsService, streakService, achievementService, learningProgressService);
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    [Test]
    public async Task HandleExerciseCompletedAsync_WhenCorrect_GrantsBaseExperiencePoints()
    {
        var userId = Guid.NewGuid();

        await _eventHandler.HandleExerciseCompletedAsync(userId, "choose_option", isCorrect: true);

        var totalAmount = await _databaseContext.UserExperiencePointsRecords
            .Where(record => record.UserId == userId && record.Source == ExperiencePointsSources.Exercise)
            .SumAsync(record => record.Amount);
        totalAmount.Should().Be(10);

        await _eventPublisher.Received().PublishExperiencePointsGrantedAsync(
            Arg.Is<ExperiencePointsGrantedEvent>(payload => payload.UserId == userId && payload.Amount == 10),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleExerciseCompletedAsync_WhenCorrect_UsesConfiguredRewardOverDefault()
    {
        var userId = Guid.NewGuid();
        _databaseContext.ExerciseTypeRewards.Add(new ExerciseTypeReward { ExerciseType = "free_text", BaseXpReward = 42 });
        await _databaseContext.SaveChangesAsync();

        await _eventHandler.HandleExerciseCompletedAsync(userId, "free_text", isCorrect: true);

        var exerciseAmount = await _databaseContext.UserExperiencePointsRecords
            .Where(record => record.UserId == userId && record.Source == ExperiencePointsSources.Exercise)
            .SumAsync(record => record.Amount);
        exerciseAmount.Should().Be(42);
    }

    [Test]
    public async Task HandleExerciseCompletedAsync_WhenIncorrect_DoesNotGrantExerciseExperiencePoints()
    {
        var userId = Guid.NewGuid();

        await _eventHandler.HandleExerciseCompletedAsync(userId, "choose_option", isCorrect: false);

        var hasExerciseExperiencePoints = await _databaseContext.UserExperiencePointsRecords
            .AnyAsync(record => record.UserId == userId && record.Source == ExperiencePointsSources.Exercise);
        hasExerciseExperiencePoints.Should().BeFalse();
    }

    [Test]
    public async Task HandleExerciseCompletedAsync_RegistersStreakActivity()
    {
        var userId = Guid.NewGuid();

        await _eventHandler.HandleExerciseCompletedAsync(userId, "choose_option", isCorrect: false);

        var streak = await _databaseContext.UserStreaks.FirstAsync(record => record.UserId == userId);
        streak.CurrentStreakDayCount.Should().Be(1);
    }

    [Test]
    public async Task HandleDialogEvaluatedAsync_GrantsDialogExperiencePoints()
    {
        var userId = Guid.NewGuid();

        await _eventHandler.HandleDialogEvaluatedAsync(userId, experiencePointsEarned: 75);

        var dialogAmount = await _databaseContext.UserExperiencePointsRecords
            .Where(record => record.UserId == userId && record.Source == ExperiencePointsSources.Dialog)
            .SumAsync(record => record.Amount);
        dialogAmount.Should().Be(75);
    }

    [Test]
    public async Task HandleLessonCompletedAsync_IncrementsLessonCountAndUnlocksFirstLesson()
    {
        var userId = Guid.NewGuid();
        _databaseContext.Achievements.Add(new Achievement
        {
            Id = Guid.NewGuid(),
            Key = "first_lesson",
            Title = "First step",
            ConditionType = AchievementConditionTypes.FirstLesson,
            ConditionThreshold = 0,
        });
        await _databaseContext.SaveChangesAsync();

        await _eventHandler.HandleLessonCompletedAsync(userId);

        var progress = await _databaseContext.UserLearningProgressRecords.FirstAsync(record => record.UserId == userId);
        progress.CompletedLessonCount.Should().Be(1);

        var unlocked = await _databaseContext.UserAchievements.AnyAsync(record => record.UserId == userId);
        unlocked.Should().BeTrue();

        await _eventPublisher.Received().PublishAchievementUnlockedAsync(
            Arg.Is<AchievementUnlockedEvent>(payload => payload.UserId == userId && payload.AchievementKey == "first_lesson"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleSkillCompletedAsync_MarksSkillAndUnlocksSkillAchievement()
    {
        var userId = Guid.NewGuid();
        _databaseContext.Achievements.Add(new Achievement
        {
            Id = Guid.NewGuid(),
            Key = "skill_completed",
            Title = "Skill master",
            ConditionType = AchievementConditionTypes.SkillCompleted,
            ConditionThreshold = 0,
        });
        await _databaseContext.SaveChangesAsync();

        await _eventHandler.HandleSkillCompletedAsync(userId);

        var progress = await _databaseContext.UserLearningProgressRecords.FirstAsync(record => record.UserId == userId);
        progress.HasCompletedAnySkill.Should().BeTrue();

        var unlocked = await _databaseContext.UserAchievements.AnyAsync(record => record.UserId == userId);
        unlocked.Should().BeTrue();
    }
}
