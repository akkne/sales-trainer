using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using SalesTrainer.Api.Features.Achievements.Services.Abstract;
using SalesTrainer.Api.Features.Notifications.Services.Abstract;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.Dialog.Services.Abstract;
using SalesTrainer.Api.Features.Exercises.Services.Implementation;
using SalesTrainer.Api.Features.Gamification.Models;
using SalesTrainer.Api.Features.Lessons.Models;
using SalesTrainer.Api.Features.SkillTree.Models;
using SalesTrainer.Api.Features.Voice.Services.Abstract;
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
            new ChooseOptionEvaluationStrategy(),
            new FillBlankEvaluationStrategy()
        ]);
        var achievementService = Substitute.For<IAchievementService>();
        var openAiChatService = Substitute.For<IOpenAiChatService>();
        var notificationService = Substitute.For<INotificationService>();
        var ttsRouter = Substitute.For<ITtsRouter>();
        // ExerciseService is internal, so NSubstitute cannot proxy ILogger<ExerciseService> — use the null logger.
        var logger = NullLogger<ExerciseService>.Instance;
        _service = new ExerciseService(_db, factory, achievementService, openAiChatService, notificationService, ttsRouter, logger);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private async Task<(Guid userId, Guid skillId, Guid topicId, Guid lessonId, Guid exerciseId)>
        SeedCoreDataAsync(int correctOptionIndex = 0, int totalLessons = 1)
    {
        var userId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
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
            IconicName = $"skill-{skillId}",
            Title = "Test Skill",
            OrderInTree = 1
        });
        _db.Topics.Add(new Topic
        {
            Id = topicId,
            SkillId = skillId,
            IconicName = $"topic-{topicId}",
            Title = "Test Topic",
            OrderInSkill = 1
        });
        _db.Lessons.Add(new Lesson
        {
            Id = lessonId,
            TopicId = topicId,
            Title = "Lesson 1",
            OrderInTopic = 1
        });
        _db.Exercises.Add(new Exercise
        {
            Id = exerciseId,
            LessonId = lessonId,
            Type = "choose_option",
            OrderInLesson = 1,
            SerializedContent = JsonSerializer.Serialize(new
            {
                situation = "Q?",
                options = new[] {
                    new { text = "A", is_correct = correctOptionIndex == 0 },
                    new { text = "B", is_correct = correctOptionIndex == 1 }
                }
            }),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
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

        return (userId, skillId, topicId, lessonId, exerciseId);
    }

    private static JsonElement AnswerJson(int index) =>
        JsonDocument.Parse(JsonSerializer.Serialize(new { selectedOptionIndex = index })).RootElement;

    [Test]
    public async Task Submit_CorrectAnswer_AddsXpRecord()
    {
        var (userId, _, _, _, exerciseId) = await SeedCoreDataAsync(correctOptionIndex: 0);

        await _service.SubmitExerciseAnswerAsync(userId, exerciseId, AnswerJson(0));

        var xp = await _db.UserXpRecords.FirstOrDefaultAsync(x => x.UserId == userId);
        xp.Should().NotBeNull();
        xp!.Amount.Should().Be(10);
    }

    [Test]
    public async Task Submit_CorrectAnswer_CreatesLessonProgressCompleted()
    {
        var (userId, _, _, lessonId, exerciseId) = await SeedCoreDataAsync(correctOptionIndex: 0);

        await _service.SubmitExerciseAnswerAsync(userId, exerciseId, AnswerJson(0));

        var progress = await _db.UserLessonProgressRecords
            .FirstOrDefaultAsync(p => p.UserId == userId && p.LessonId == lessonId);
        progress.Should().NotBeNull();
        progress!.Status.Should().Be("completed");
    }

    [Test]
    public async Task Submit_CorrectAnswer_UpdatesExistingLessonProgress()
    {
        var (userId, _, _, lessonId, exerciseId) = await SeedCoreDataAsync(correctOptionIndex: 0);
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
    public async Task Submit_CorrectAnswer_FirstActivity_CreatesStreak()
    {
        var (userId, _, _, _, exerciseId) = await SeedCoreDataAsync(correctOptionIndex: 0);

        await _service.SubmitExerciseAnswerAsync(userId, exerciseId, AnswerJson(0));

        var streak = await _db.UserStreaks.FirstOrDefaultAsync(s => s.UserId == userId);
        streak.Should().NotBeNull();
        streak!.CurrentStreakDayCount.Should().Be(1);
    }

    [Test]
    public async Task Submit_CorrectAnswer_ConsecutiveDay_IncrementsStreak()
    {
        var (userId, _, _, _, exerciseId) = await SeedCoreDataAsync(correctOptionIndex: 0);

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
        var (userId, _, _, _, exerciseId) = await SeedCoreDataAsync(correctOptionIndex: 1);

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

    private async Task<(Guid userId, string skillSlug, Guid topicId, Guid lessonId1, Guid lessonId2, Guid lessonId3)>
        SeedSkillWithThreeLessonsAsync()
    {
        var userId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
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
            Id = skillId, IconicName = slug, Title = "S", OrderInTree = 1
        });
        _db.Topics.Add(new Topic
        {
            Id = topicId, SkillId = skillId, IconicName = $"topic-{topicId}", Title = "T", OrderInSkill = 1
        });
        _db.Lessons.Add(new Lesson { Id = lesson1Id, TopicId = topicId, Title = "L1", OrderInTopic = 1 });
        _db.Lessons.Add(new Lesson { Id = lesson2Id, TopicId = topicId, Title = "L2", OrderInTopic = 2 });
        _db.Lessons.Add(new Lesson { Id = lesson3Id, TopicId = topicId, Title = "L3", OrderInTopic = 3 });
        _db.UserSkillProgressRecords.Add(new UserSkillProgress
        {
            Id = Guid.NewGuid(), UserId = userId, SkillId = skillId,
            Status = "available", CompletedLessonCount = 0, TotalLessonCount = 3
        });
        await _db.SaveChangesAsync();

        return (userId, slug, topicId, lesson1Id, lesson2Id, lesson3Id);
    }

    [Test]
    public async Task GetLessonsForSkill_FirstAccess_SeedsFirstLessonAsAvailable()
    {
        var (userId, slug, _, lesson1Id, _, _) = await SeedSkillWithThreeLessonsAsync();

        var lessons = await _service.GetLessonsForSkillAsync(userId, slug);

        lessons.First(l => l.LessonId == lesson1Id).Status.Should().Be("available");
    }

    [Test]
    public async Task GetLessonsForSkill_FirstAccess_SeedsRemainingLessonsAsLocked()
    {
        var (userId, slug, _, _, lesson2Id, lesson3Id) = await SeedSkillWithThreeLessonsAsync();

        var lessons = await _service.GetLessonsForSkillAsync(userId, slug);

        lessons.First(l => l.LessonId == lesson2Id).Status.Should().Be("locked");
        lessons.First(l => l.LessonId == lesson3Id).Status.Should().Be("locked");
    }

    [Test]
    public async Task GetLessonsForSkill_LockedSkill_FirstLessonStillAvailable()
    {
        // Current implementation doesn't check UserSkillProgress status —
        // lessons are always returned based on user lesson progress only.
        var userId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var slug = $"locked-{skillId}";

        _db.Users.Add(new User
        {
            Id = userId, Email = $"u{userId}@test.com", DisplayName = "T", CreatedAt = DateTime.UtcNow
        });
        _db.Skills.Add(new Skill
        {
            Id = skillId, IconicName = slug, Title = "S", OrderInTree = 2
        });
        _db.Topics.Add(new Topic
        {
            Id = topicId, SkillId = skillId, IconicName = $"topic-{topicId}", Title = "T", OrderInSkill = 1
        });
        _db.Lessons.Add(new Lesson { Id = Guid.NewGuid(), TopicId = topicId, Title = "L", OrderInTopic = 1 });
        _db.UserSkillProgressRecords.Add(new UserSkillProgress
        {
            Id = Guid.NewGuid(), UserId = userId, SkillId = skillId,
            Status = "locked", CompletedLessonCount = 0, TotalLessonCount = 1
        });
        await _db.SaveChangesAsync();

        var lessons = await _service.GetLessonsForSkillAsync(userId, slug);

        lessons.Should().HaveCount(1);
        // First lesson is available regardless of skill status
        lessons[0].Status.Should().Be("available");
    }

    [Test]
    public async Task GetLessonsForSkill_AlreadyHasProgress_DoesNotReseed()
    {
        var (userId, slug, _, lesson1Id, lesson2Id, _) = await SeedSkillWithThreeLessonsAsync();

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
    public async Task Submit_CorrectAnswer_UnlocksNextLessonInTopic()
    {
        var (userId, _, topicId, lesson1Id, exerciseId) = await SeedCoreDataAsync(
            correctOptionIndex: 0, totalLessons: 2);

        var lesson2Id = Guid.NewGuid();
        _db.Lessons.Add(new Lesson
        {
            Id = lesson2Id, TopicId = topicId, Title = "Lesson 2",
            OrderInTopic = 2
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
        var (userId, _, _, _, exerciseId) = await SeedCoreDataAsync(
            correctOptionIndex: 0, totalLessons: 1);

        var act = async () => await _service.SubmitExerciseAnswerAsync(userId, exerciseId, AnswerJson(0));

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task Submit_CorrectAnswer_UnlocksNextLesson_WhenNoProgressRecordExists()
    {
        // Next lesson has no progress record yet (wasn't seeded) — still gets unlocked
        var (userId, _, topicId, _, exerciseId) = await SeedCoreDataAsync(
            correctOptionIndex: 0, totalLessons: 2);

        var lesson2Id = Guid.NewGuid();
        _db.Lessons.Add(new Lesson
        {
            Id = lesson2Id, TopicId = topicId, Title = "Lesson 2",
            OrderInTopic = 2
        });
        await _db.SaveChangesAsync();

        await _service.SubmitExerciseAnswerAsync(userId, exerciseId, AnswerJson(0));

        var next = await _db.UserLessonProgressRecords
            .FirstOrDefaultAsync(p => p.UserId == userId && p.LessonId == lesson2Id);
        next.Should().NotBeNull();
        next!.Status.Should().Be("available");
    }
}
