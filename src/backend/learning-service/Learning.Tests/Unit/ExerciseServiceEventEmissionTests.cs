using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Eventing;
using Sellevate.Learning.Features.Exercises.Services.Abstract;
using Sellevate.Learning.Features.Exercises.Services.Implementation;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.Lessons.Services.Implementation;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Infrastructure.Ai;
using Sellevate.Learning.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sellevate.Learning.Tests.Helpers;

namespace Sellevate.Learning.Tests.Unit;

[TestFixture]
public sealed class ExerciseServiceEventEmissionTests
{
    private static ExerciseEvaluationFactory CreateFactory(LearningDbContext databaseContext)
    {
        var deterministicStrategies = new IExerciseEvaluationStrategy[]
        {
            new ChooseOptionEvaluationStrategy(),
            new FillBlankEvaluationStrategy(),
            new ReorderEvaluationStrategy(),
            new MatchPairsEvaluationStrategy(),
            new CategorizeEvaluationStrategy(),
            new TheoryCardEvaluationStrategy(),
        };

        return new ExerciseEvaluationFactory(
            deterministicStrategies,
            Substitute.For<IAiEvaluationClient>(),
            databaseContext,
            new StubOrganizationProfileProvider());
    }

    private static async Task<(Guid SkillId, Guid LessonId, Guid ExerciseId)> SeedSingleLessonSkillAsync(
        LearningDbContext databaseContext)
    {
        var skillId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();

        databaseContext.Skills.Add(new Skill { Id = skillId, IconicName = "cold-calling", Title = "Cold calling" });
        databaseContext.Topics.Add(new Topic { Id = topicId, SkillId = skillId, IconicName = "basics", Title = "Basics" });
        databaseContext.Lessons.Add(new Lesson { Id = lessonId, TopicId = topicId, Title = "Opening", OrderInTopic = 1 });
        databaseContext.Exercises.Add(new Exercise
        {
            Id = exerciseId,
            LessonId = lessonId,
            Type = ExerciseTypes.ChooseOption,
            OrderInLesson = 1,
            SerializedContent = """{"options":[{"text":"a","is_correct":true},{"text":"b","is_correct":false}]}""",
        });
        await databaseContext.SaveChangesAsync();

        return (skillId, lessonId, exerciseId);
    }

    /// <summary>
    /// Regression: finishing a topic's last lesson must roll over and unlock the first lesson of the next
    /// topic, not leave it locked.
    /// </summary>
    [Test]
    public async Task CompletingLastLessonInTopic_UnlocksFirstLessonOfNextTopic()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();

        var skillId = Guid.NewGuid();
        var topic1Id = Guid.NewGuid();
        var topic2Id = Guid.NewGuid();
        var lesson1Id = Guid.NewGuid();
        var lesson2Id = Guid.NewGuid();
        var exercise1Id = Guid.NewGuid();

        databaseContext.Skills.Add(new Skill { Id = skillId, IconicName = "cold-calling", Title = "Cold calling" });
        databaseContext.Topics.Add(new Topic { Id = topic1Id, SkillId = skillId, IconicName = "basics", Title = "Basics", OrderInSkill = 1 });
        databaseContext.Topics.Add(new Topic { Id = topic2Id, SkillId = skillId, IconicName = "advanced", Title = "Advanced", OrderInSkill = 2 });
        databaseContext.Lessons.Add(new Lesson { Id = lesson1Id, TopicId = topic1Id, Title = "Opening", OrderInTopic = 1 });
        databaseContext.Lessons.Add(new Lesson { Id = lesson2Id, TopicId = topic2Id, Title = "Objections", OrderInTopic = 1 });
        databaseContext.Exercises.Add(new Exercise
        {
            Id = exercise1Id,
            LessonId = lesson1Id,
            Type = ExerciseTypes.ChooseOption,
            OrderInLesson = 1,
            SerializedContent = """{"options":[{"text":"a","is_correct":true},{"text":"b","is_correct":false}]}""",
        });
        await databaseContext.SaveChangesAsync();

        var service = new ExerciseService(
            databaseContext, CreateFactory(databaseContext),
            Substitute.For<ILearningEventPublisher>(),
            Substitute.For<IExerciseDialogService>(),
            new LessonVersionService(databaseContext),
            new StubOrganizationProfileProvider(),
            NullLogger<ExerciseService>.Instance);

        var userId = Guid.NewGuid();
        var answer = JsonDocument.Parse("""{"selectedOptionIndex":0}""").RootElement;

        await service.SubmitExerciseAnswerAsync(userId, exercise1Id, answer);

        var lesson2Progress = databaseContext.UserLessonProgressRecords
            .SingleOrDefault(record => record.UserId == userId && record.LessonId == lesson2Id);

        lesson2Progress.Should().NotBeNull("finishing the last lesson of topic 1 should unlock topic 2");
        lesson2Progress!.Status.Should().Be(LessonProgressStatuses.Available);
    }

    [Test]
    public async Task SubmitCorrectAnswer_EmitsExerciseLessonAndSkillCompletedEvents()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();
        var (skillId, lessonId, exerciseId) = await SeedSingleLessonSkillAsync(databaseContext);

        var eventPublisher = Substitute.For<ILearningEventPublisher>();
        var dialogService = Substitute.For<IExerciseDialogService>();

        var service = new ExerciseService(
            databaseContext, CreateFactory(databaseContext), eventPublisher, dialogService,
            new LessonVersionService(databaseContext),
            new StubOrganizationProfileProvider(),
            NullLogger<ExerciseService>.Instance);

        var userId = Guid.NewGuid();
        var answer = JsonDocument.Parse("""{"selectedOptionIndex":0}""").RootElement;

        var result = await service.SubmitExerciseAnswerAsync(userId, exerciseId, answer);

        result.IsCorrect.Should().BeTrue();
        result.XpEarned.Should().Be(0);

        await eventPublisher.Received(1).PublishExerciseCompletedAsync(
            Arg.Is<ExerciseCompletedEvent>(payload =>
                payload.UserId == userId
                && payload.ExerciseType == ExerciseTypes.ChooseOption
                && payload.IsCorrect
                && payload.Score == 100),
            Arg.Any<CancellationToken>());

        await eventPublisher.Received(1).PublishLessonCompletedAsync(
            Arg.Is<LessonCompletedEvent>(payload =>
                payload.UserId == userId && payload.LessonId == lessonId && payload.BestScore == 100),
            Arg.Any<CancellationToken>());

        await eventPublisher.Received(1).PublishSkillCompletedAsync(
            Arg.Is<SkillCompletedEvent>(payload =>
                payload.UserId == userId && payload.SkillId == skillId),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Lesson completion is <b>attempt-based</b>: attempting the only exercise, even wrongly, means every
    /// exercise has been attempted, so the lesson can still be passed.
    /// </summary>
    [Test]
    public async Task SubmitWrongAnswer_SingleExerciseLesson_StillCompletesLesson()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();
        var (skillId, lessonId, exerciseId) = await SeedSingleLessonSkillAsync(databaseContext);

        var eventPublisher = Substitute.For<ILearningEventPublisher>();
        var service = new ExerciseService(
            databaseContext, CreateFactory(databaseContext), eventPublisher,
            Substitute.For<IExerciseDialogService>(),
            new LessonVersionService(databaseContext),
            new StubOrganizationProfileProvider(),
            NullLogger<ExerciseService>.Instance);

        var userId = Guid.NewGuid();
        var answer = JsonDocument.Parse("""{"selectedOptionIndex":1}""").RootElement;

        var result = await service.SubmitExerciseAnswerAsync(userId, exerciseId, answer);

        result.IsCorrect.Should().BeFalse();

        await eventPublisher.Received(1).PublishExerciseCompletedAsync(
            Arg.Is<ExerciseCompletedEvent>(payload => !payload.IsCorrect), Arg.Any<CancellationToken>());
        await eventPublisher.Received(1).PublishLessonCompletedAsync(
            Arg.Is<LessonCompletedEvent>(payload =>
                payload.UserId == userId && payload.LessonId == lessonId),
            Arg.Any<CancellationToken>());
        await eventPublisher.Received(1).PublishSkillCompletedAsync(
            Arg.Is<SkillCompletedEvent>(payload =>
                payload.UserId == userId && payload.SkillId == skillId),
            Arg.Any<CancellationToken>());
    }
}
