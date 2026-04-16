using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NUnit.Framework;
using SalesTrainer.Api.Features.Achievements.Services.Abstract;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.Dialog.Services.Abstract;
using SalesTrainer.Api.Features.Exercises.Services.Implementation;
using SalesTrainer.Api.Features.Gamification.Models;
using SalesTrainer.Api.Features.Lessons.Models;
using SalesTrainer.Api.Features.SkillTree.Models;
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
        var achievementService = Substitute.For<IAchievementService>();
        var openAiChatService = Substitute.For<IOpenAiChatService>();
        _service = new ExerciseService(_db, factory, achievementService, openAiChatService);
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

        _db.Users.Add(new User
        {
            Id = userId,
            Email = $"u{userId}@test.com",
            DisplayName = "Test",
            CreatedAt = DateTime.UtcNow
        });
        _db.Skills.Add(new Skill
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
        _db.Skills.Add(new Skill
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

    // ──────────────────────────────────────────────────────────────────────────
    // Lesson progress seeding tests
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<(Guid userId, string skillSlug, Guid lessonId1, Guid lessonId2, Guid lessonId3)>
        SeedSkillWithThreeLessonsAsync()
    {
        var userId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var slug = $"skill-{skillId}";
        var lesson1Id = Guid.NewGuid();
        var lesson2Id = Guid.NewGuid();
        var lesson3Id = Guid.NewGuid();

        _db.Users.Add(new User
        {
            Id = userId, Email = $"u{userId}@test.com", DisplayName = "T", CreatedAt = DateTime.UtcNow
        });
        _db.Skills.Add(new Skill
        {
            Id = skillId, Slug = slug, Title = "S", IconName = "i", SortOrder = 1,
            ApplicableSalesTypes = ["all"]
        });
        _db.Lessons.Add(new Lesson { Id = lesson1Id, SkillId = skillId, Title = "L1", SortOrder = 1, DifficultyLevel = 1, XpReward = 10 });
        _db.Lessons.Add(new Lesson { Id = lesson2Id, SkillId = skillId, Title = "L2", SortOrder = 2, DifficultyLevel = 1, XpReward = 10 });
        _db.Lessons.Add(new Lesson { Id = lesson3Id, SkillId = skillId, Title = "L3", SortOrder = 3, DifficultyLevel = 1, XpReward = 10 });
        _db.UserSkillProgressRecords.Add(new UserSkillProgress
        {
            Id = Guid.NewGuid(), UserId = userId, SkillId = skillId,
            Status = "available", CompletedLessonCount = 0, TotalLessonCount = 3
        });
        await _db.SaveChangesAsync();

        return (userId, slug, lesson1Id, lesson2Id, lesson3Id);
    }

    [Test]
    public async Task GetLessonsForSkill_FirstAccess_SeedsFirstLessonAsAvailable()
    {
        var (userId, slug, lesson1Id, _, _) = await SeedSkillWithThreeLessonsAsync();

        var lessons = await _service.GetLessonsForSkillAsync(userId, slug);

        lessons.First(l => l.LessonId == lesson1Id).Status.Should().Be("available");
    }

    [Test]
    public async Task GetLessonsForSkill_FirstAccess_SeedsRemainingLessonsAsLocked()
    {
        var (userId, slug, _, lesson2Id, lesson3Id) = await SeedSkillWithThreeLessonsAsync();

        var lessons = await _service.GetLessonsForSkillAsync(userId, slug);

        lessons.First(l => l.LessonId == lesson2Id).Status.Should().Be("locked");
        lessons.First(l => l.LessonId == lesson3Id).Status.Should().Be("locked");
    }

    [Test]
    public async Task GetLessonsForSkill_LockedSkill_DoesNotSeedLessonProgress()
    {
        var userId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var slug = $"locked-{skillId}";

        _db.Users.Add(new SalesTrainer.Api.Features.Auth.User
        {
            Id = userId, Email = $"u{userId}@test.com", DisplayName = "T", CreatedAt = DateTime.UtcNow
        });
        _db.Skills.Add(new SalesTrainer.Api.Features.SkillTree.Skill
        {
            Id = skillId, Slug = slug, Title = "S", IconName = "i", SortOrder = 2,
            ApplicableSalesTypes = ["all"]
        });
        _db.Lessons.Add(new Lesson { Id = Guid.NewGuid(), SkillId = skillId, Title = "L", SortOrder = 1, DifficultyLevel = 1, XpReward = 10 });
        _db.UserSkillProgressRecords.Add(new UserSkillProgress
        {
            Id = Guid.NewGuid(), UserId = userId, SkillId = skillId,
            Status = "locked", CompletedLessonCount = 0, TotalLessonCount = 1
        });
        await _db.SaveChangesAsync();

        var lessons = await _service.GetLessonsForSkillAsync(userId, slug);

        lessons.Should().HaveCount(1);
        lessons[0].Status.Should().Be("locked");
        var progressCount = await _db.UserLessonProgressRecords
            .CountAsync(p => p.UserId == userId);
        progressCount.Should().Be(0);
    }

    [Test]
    public async Task GetLessonsForSkill_AlreadyHasProgress_DoesNotReseed()
    {
        var (userId, slug, lesson1Id, lesson2Id, _) = await SeedSkillWithThreeLessonsAsync();

        // Pre-existing progress: lesson 1 completed, lesson 2 available
        _db.UserLessonProgressRecords.Add(new UserLessonProgress
        {
            Id = Guid.NewGuid(), UserId = userId, LessonId = lesson1Id, Status = "completed", BestScore = 100
        });
        _db.UserLessonProgressRecords.Add(new UserLessonProgress
        {
            Id = Guid.NewGuid(), UserId = userId, LessonId = lesson2Id, Status = "available", BestScore = 0
        });
        await _db.SaveChangesAsync();

        var lessons = await _service.GetLessonsForSkillAsync(userId, slug);

        lessons.First(l => l.LessonId == lesson1Id).Status.Should().Be("completed");
        lessons.First(l => l.LessonId == lesson2Id).Status.Should().Be("available");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Next-lesson unlock tests
    // ──────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task Submit_CorrectAnswer_UnlocksNextLessonInSkill()
    {
        var (userId, skillId, lesson1Id, exerciseId) = await SeedCoreDataAsync(
            correctOptionIndex: 0, totalLessons: 2);

        var lesson2Id = Guid.NewGuid();
        _db.Lessons.Add(new Lesson
        {
            Id = lesson2Id, SkillId = skillId, Title = "Lesson 2",
            SortOrder = 2, DifficultyLevel = 1, XpReward = 10
        });
        _db.UserLessonProgressRecords.Add(new UserLessonProgress
        {
            Id = Guid.NewGuid(), UserId = userId, LessonId = lesson1Id,
            Status = "available", BestScore = 0
        });
        _db.UserLessonProgressRecords.Add(new UserLessonProgress
        {
            Id = Guid.NewGuid(), UserId = userId, LessonId = lesson2Id,
            Status = "locked", BestScore = 0
        });
        await _db.SaveChangesAsync();

        await _service.SubmitExerciseAnswerAsync(userId, exerciseId, AnswerJson(0));

        var next = await _db.UserLessonProgressRecords
            .FirstAsync(p => p.UserId == userId && p.LessonId == lesson2Id);
        next.Status.Should().Be("available");
    }

    [Test]
    public async Task Submit_CorrectAnswer_LastLesson_DoesNotThrow()
    {
        var (userId, _, _, exerciseId) = await SeedCoreDataAsync(
            correctOptionIndex: 0, totalLessons: 1);

        var act = async () => await _service.SubmitExerciseAnswerAsync(userId, exerciseId, AnswerJson(0));

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task Submit_CorrectAnswer_UnlocksNextLesson_WhenNoProgressRecordExists()
    {
        // Next lesson has no progress record yet (wasn't seeded) — still gets unlocked
        var (userId, skillId, _, exerciseId) = await SeedCoreDataAsync(
            correctOptionIndex: 0, totalLessons: 2);

        var lesson2Id = Guid.NewGuid();
        _db.Lessons.Add(new Lesson
        {
            Id = lesson2Id, SkillId = skillId, Title = "Lesson 2",
            SortOrder = 2, DifficultyLevel = 1, XpReward = 10
        });
        await _db.SaveChangesAsync();

        await _service.SubmitExerciseAnswerAsync(userId, exerciseId, AnswerJson(0));

        var next = await _db.UserLessonProgressRecords
            .FirstOrDefaultAsync(p => p.UserId == userId && p.LessonId == lesson2Id);
        next.Should().NotBeNull();
        next!.Status.Should().Be("available");
    }
}
