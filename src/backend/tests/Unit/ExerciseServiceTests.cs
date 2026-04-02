using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using SalesTrainer.Api.Features.Exercises;
using SalesTrainer.Api.Features.Gamification;
using SalesTrainer.Api.Features.Lessons;
using SalesTrainer.Api.Features.SkillTree;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Unit;

[TestFixture]
public class ExerciseServiceTests
{
    private AppDbContext _db = null!;
    private ExerciseService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _db = InMemoryDbContextFactory.Create();
        var factory = new ExerciseEvaluationFactory([
            new MultipleChoiceEvaluationStrategy(),
            new FillBlankEvaluationStrategy()
        ]);
        _service = new ExerciseService(_db, factory);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private async Task<(Guid userId, Guid skillId, Guid lessonId, Guid exerciseId)>
        SeedCoreDataAsync(int correctOptionIndex = 0, int totalLessons = 1)
    {
        var userId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();

        _db.Users.Add(new SalesTrainer.Api.Features.Auth.User
        {
            Id = userId,
            Email = $"u{userId}@test.com",
            DisplayName = "Test",
            CreatedAt = DateTime.UtcNow
        });
        _db.Skills.Add(new SalesTrainer.Api.Features.SkillTree.Skill
        {
            Id = skillId,
            Slug = $"skill-{skillId}",
            Title = "Test Skill",
            IconName = "icon",
            SortOrder = 1,
            ApplicableSalesTypes = ["enterprise"]
        });
        _db.Lessons.Add(new Lesson
        {
            Id = lessonId,
            SkillId = skillId,
            Title = "Lesson 1",
            SortOrder = 1,
            DifficultyLevel = 1,
            XpReward = 50
        });
        _db.Exercises.Add(new Exercise
        {
            Id = exerciseId,
            LessonId = lessonId,
            Type = "multiple_choice",
            SortOrder = 1,
            SerializedContent = JsonSerializer.Serialize(new
            {
                question = "Q?",
                options = new[] { "A", "B" },
                correctOptionIndex
            })
        });
        _db.UserSkillProgressRecords.Add(new UserSkillProgress
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SkillId = skillId,
            Status = "available",
            CompletedLessonCount = 0,
            TotalLessonCount = totalLessons
        });
        await _db.SaveChangesAsync();

        return (userId, skillId, lessonId, exerciseId);
    }

    private static JsonElement AnswerJson(int index) =>
        JsonDocument.Parse(JsonSerializer.Serialize(new { selectedOptionIndex = index })).RootElement;

    [Test]
    public async Task Submit_CorrectAnswer_AddsXpRecord()
    {
        var (userId, _, _, exerciseId) = await SeedCoreDataAsync(correctOptionIndex: 0);

        await _service.SubmitExerciseAnswerAsync(userId, exerciseId, AnswerJson(0));

        var xp = await _db.UserXpRecords.FirstOrDefaultAsync(x => x.UserId == userId);
        xp.Should().NotBeNull();
        xp!.Amount.Should().Be(50);
    }

    [Test]
    public async Task Submit_CorrectAnswer_CreatesLessonProgressCompleted()
    {
        var (userId, _, lessonId, exerciseId) = await SeedCoreDataAsync(correctOptionIndex: 0);

        await _service.SubmitExerciseAnswerAsync(userId, exerciseId, AnswerJson(0));

        var progress = await _db.UserLessonProgressRecords
            .FirstOrDefaultAsync(p => p.UserId == userId && p.LessonId == lessonId);
        progress.Should().NotBeNull();
        progress!.Status.Should().Be("completed");
    }

    [Test]
    public async Task Submit_CorrectAnswer_UpdatesExistingLessonProgress()
    {
        var (userId, _, lessonId, exerciseId) = await SeedCoreDataAsync(correctOptionIndex: 0);
        _db.UserLessonProgressRecords.Add(new UserLessonProgress
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LessonId = lessonId,
            Status = "in_progress",
            BestScore = 0
        });
        await _db.SaveChangesAsync();

        await _service.SubmitExerciseAnswerAsync(userId, exerciseId, AnswerJson(0));

        var progress = await _db.UserLessonProgressRecords
            .FirstAsync(p => p.UserId == userId && p.LessonId == lessonId);
        progress.Status.Should().Be("completed");
        progress.BestScore.Should().Be(100);
    }

    [Test]
    public async Task Submit_CorrectAnswer_LastLesson_SetsSkillCompleted()
    {
        var (userId, skillId, _, exerciseId) = await SeedCoreDataAsync(
            correctOptionIndex: 0, totalLessons: 1);

        await _service.SubmitExerciseAnswerAsync(userId, exerciseId, AnswerJson(0));

        var skillProgress = await _db.UserSkillProgressRecords
            .FirstAsync(p => p.UserId == userId && p.SkillId == skillId);
        skillProgress.Status.Should().Be("completed");
    }

    [Test]
    public async Task Submit_SkillCompleted_UnlocksNextPrerequisiteSkill()
    {
        var (userId, skillId, _, exerciseId) = await SeedCoreDataAsync(
            correctOptionIndex: 0, totalLessons: 1);

        var nextSkillId = Guid.NewGuid();
        _db.Skills.Add(new SalesTrainer.Api.Features.SkillTree.Skill
        {
            Id = nextSkillId,
            Slug = $"next-{nextSkillId}",
            Title = "Next Skill",
            IconName = "icon",
            SortOrder = 2,
            PrerequisiteSkillId = skillId,
            ApplicableSalesTypes = ["enterprise"]
        });
        _db.UserSkillProgressRecords.Add(new UserSkillProgress
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SkillId = nextSkillId,
            Status = "locked",
            CompletedLessonCount = 0,
            TotalLessonCount = 1
        });
        await _db.SaveChangesAsync();

        await _service.SubmitExerciseAnswerAsync(userId, exerciseId, AnswerJson(0));

        var nextProgress = await _db.UserSkillProgressRecords
            .FirstAsync(p => p.UserId == userId && p.SkillId == nextSkillId);
        nextProgress.Status.Should().Be("available");
    }

    [Test]
    public async Task Submit_CorrectAnswer_FirstActivity_CreatesStreak()
    {
        var (userId, _, _, exerciseId) = await SeedCoreDataAsync(correctOptionIndex: 0);

        await _service.SubmitExerciseAnswerAsync(userId, exerciseId, AnswerJson(0));

        var streak = await _db.UserStreaks.FirstOrDefaultAsync(s => s.UserId == userId);
        streak.Should().NotBeNull();
        streak!.CurrentStreakDayCount.Should().Be(1);
    }

    [Test]
    public async Task Submit_CorrectAnswer_ConsecutiveDay_IncrementsStreak()
    {
        var (userId, _, _, exerciseId) = await SeedCoreDataAsync(correctOptionIndex: 0);

        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        _db.UserStreaks.Add(new UserStreak
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentStreakDayCount = 3,
            LongestStreakDayCount = 3,
            LastActivityDate = yesterday
        });
        await _db.SaveChangesAsync();

        await _service.SubmitExerciseAnswerAsync(userId, exerciseId, AnswerJson(0));

        var streak = await _db.UserStreaks.FirstAsync(s => s.UserId == userId);
        streak.CurrentStreakDayCount.Should().Be(4);
    }

    [Test]
    public async Task Submit_IncorrectAnswer_NoXpRecord()
    {
        var (userId, _, _, exerciseId) = await SeedCoreDataAsync(correctOptionIndex: 1);

        await _service.SubmitExerciseAnswerAsync(userId, exerciseId, AnswerJson(0));

        var xpCount = await _db.UserXpRecords.CountAsync(x => x.UserId == userId);
        xpCount.Should().Be(0);
    }

    [Test]
    public async Task Submit_UnknownExercise_ThrowsKeyNotFoundException()
    {
        var act = async () =>
            await _service.SubmitExerciseAnswerAsync(Guid.NewGuid(), Guid.NewGuid(), AnswerJson(0));

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
