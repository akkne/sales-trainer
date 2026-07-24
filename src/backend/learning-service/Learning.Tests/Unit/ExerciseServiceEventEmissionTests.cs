using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Eventing;
using Sellevate.Learning.Features.Exercises.Services.Abstract;
using Sellevate.Learning.Features.Exercises.Services.Implementation;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Infrastructure.Ai;
using Sellevate.Learning.Infrastructure.Data;

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
            databaseContext);
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

    [Test]
    public async Task SubmitCorrectAnswer_EmitsExerciseLessonAndSkillCompletedEvents()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();
        var (skillId, lessonId, exerciseId) = await SeedSingleLessonSkillAsync(databaseContext);

        var eventPublisher = Substitute.For<ILearningEventPublisher>();
        var dialogService = Substitute.For<IExerciseDialogService>();

        var service = new ExerciseService(
            databaseContext, CreateFactory(databaseContext), eventPublisher, dialogService);

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

    [Test]
    public async Task SubmitWrongAnswer_SingleExerciseLesson_StillCompletesLesson()
    {
        // Lesson completion is attempt-based: attempting the only exercise (even wrongly)
        // means every exercise has been attempted, so the lesson can still be passed.
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();
        var (skillId, lessonId, exerciseId) = await SeedSingleLessonSkillAsync(databaseContext);

        var eventPublisher = Substitute.For<ILearningEventPublisher>();
        var service = new ExerciseService(
            databaseContext, CreateFactory(databaseContext), eventPublisher,
            Substitute.For<IExerciseDialogService>());

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
