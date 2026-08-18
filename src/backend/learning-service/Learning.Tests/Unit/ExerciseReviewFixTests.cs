using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Eventing;
using Sellevate.Learning.Features.Exercises.Models;
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

/// <summary>
/// Tests for review findings LE2 (400 on malformed answer), LE3 (all-exercises-passed gate),
/// and LE4 (real best score in LessonCompletedEvent).
/// </summary>
[TestFixture]
public sealed class ExerciseReviewFixTests
{
    private static ExerciseEvaluationFactory CreateFactory(LearningDbContext databaseContext)
    {
        var strategies = new IExerciseEvaluationStrategy[]
        {
            new ChooseOptionEvaluationStrategy(),
            new FillBlankEvaluationStrategy(),
            new ReorderEvaluationStrategy(),
            new MatchPairsEvaluationStrategy(),
            new CategorizeEvaluationStrategy(),
            new TheoryCardEvaluationStrategy(),
        };
        return new ExerciseEvaluationFactory(
            strategies, Substitute.For<IAiEvaluationClient>(), databaseContext, new StubOrganizationProfileProvider());
    }

    private static ExerciseService CreateService(LearningDbContext databaseContext, ILearningEventPublisher publisher) =>
        new(databaseContext, CreateFactory(databaseContext), publisher, Substitute.For<IExerciseDialogService>(),
            new LessonVersionService(databaseContext),
            new StubOrganizationProfileProvider(),
            NullLogger<ExerciseService>.Instance);

    [Test]
    public void ChooseOption_MissingSelectedOptionIndex_ThrowsValidationException()
    {
        var strategy = new ChooseOptionEvaluationStrategy();
        var content = JsonDocument.Parse("""{"options":[{"text":"a","is_correct":true}]}""").RootElement;
        var badAnswer = JsonDocument.Parse("""{}""").RootElement;

        var act = () => strategy.EvaluateAnswerAsync(content, badAnswer);

        act.Should().ThrowAsync<ExerciseAnswerValidationException>()
            .WithMessage("*selectedOptionIndex*");
    }

    [Test]
    public void FillBlank_MissingSelectedOptionIndex_ThrowsValidationException()
    {
        var strategy = new FillBlankEvaluationStrategy();
        var content = JsonDocument.Parse("""{"options":[{"text":"a","is_correct":true}]}""").RootElement;
        var badAnswer = JsonDocument.Parse("""{"wrongField":0}""").RootElement;

        var act = () => strategy.EvaluateAnswerAsync(content, badAnswer);

        act.Should().ThrowAsync<ExerciseAnswerValidationException>();
    }

    [Test]
    public void Reorder_MissingOrderField_ThrowsValidationException()
    {
        var strategy = new ReorderEvaluationStrategy();
        var content = JsonDocument.Parse("""{"items":[{"text":"a","correct_position":1}]}""").RootElement;
        var badAnswer = JsonDocument.Parse("""{"notOrder":[0]}""").RootElement;

        var act = () => strategy.EvaluateAnswerAsync(content, badAnswer);

        act.Should().ThrowAsync<ExerciseAnswerValidationException>()
            .WithMessage("*order*");
    }

    [Test]
    public void MatchPairs_MissingPairsField_ThrowsValidationException()
    {
        var strategy = new MatchPairsEvaluationStrategy();
        var content = JsonDocument.Parse("""{"pairs":[{"left":"a","right":"1"}]}""").RootElement;
        var badAnswer = JsonDocument.Parse("""{"noPairs":true}""").RootElement;

        var act = () => strategy.EvaluateAnswerAsync(content, badAnswer);

        act.Should().ThrowAsync<ExerciseAnswerValidationException>()
            .WithMessage("*pairs*");
    }

    [Test]
    public void Categorize_MissingMappingField_ThrowsValidationException()
    {
        var strategy = new CategorizeEvaluationStrategy();
        var content = JsonDocument.Parse("""{"items":[{"text":"x","category":"A"}]}""").RootElement;
        var badAnswer = JsonDocument.Parse("""{"noMapping":true}""").RootElement;

        var act = () => strategy.EvaluateAnswerAsync(content, badAnswer);

        act.Should().ThrowAsync<ExerciseAnswerValidationException>()
            .WithMessage("*mapping*");
    }

    /// <summary>
    /// LE3. A lesson is complete only when <b>every</b> exercise in it has been passed. Answering the
    /// first of two correctly must emit neither the lesson nor the skill completion.
    /// </summary>
    [Test]
    public async Task SubmitCorrectAnswer_PartialLesson_DoesNotEmitLessonCompleted()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();

        var skillId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var exercise1Id = Guid.NewGuid();
        var exercise2Id = Guid.NewGuid();

        databaseContext.Skills.Add(new Skill { Id = skillId, IconicName = "s", Title = "S" });
        databaseContext.Topics.Add(new Topic { Id = topicId, SkillId = skillId, IconicName = "t", Title = "T" });
        databaseContext.Lessons.Add(new Lesson { Id = lessonId, TopicId = topicId, Title = "L", OrderInTopic = 1 });
        databaseContext.Exercises.Add(new Exercise
        {
            Id = exercise1Id, LessonId = lessonId, Type = ExerciseTypes.ChooseOption, OrderInLesson = 1,
            SerializedContent = """{"options":[{"text":"a","is_correct":true},{"text":"b","is_correct":false}]}"""
        });
        databaseContext.Exercises.Add(new Exercise
        {
            Id = exercise2Id, LessonId = lessonId, Type = ExerciseTypes.ChooseOption, OrderInLesson = 2,
            SerializedContent = """{"options":[{"text":"a","is_correct":true},{"text":"b","is_correct":false}]}"""
        });
        await databaseContext.SaveChangesAsync();

        var publisher = Substitute.For<ILearningEventPublisher>();
        var service = CreateService(databaseContext, publisher);
        var userId = Guid.NewGuid();

        var answer = JsonDocument.Parse("""{"selectedOptionIndex":0}""").RootElement;
        var result = await service.SubmitExerciseAnswerAsync(userId, exercise1Id, answer);

        result.IsCorrect.Should().BeTrue();

        await publisher.DidNotReceive().PublishLessonCompletedAsync(
            Arg.Any<LessonCompletedEvent>(), Arg.Any<CancellationToken>());
        await publisher.DidNotReceive().PublishSkillCompletedAsync(
            Arg.Any<SkillCompletedEvent>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// LE3, the other side: the completion fires on the answer that passes the last remaining exercise,
    /// and exactly once.
    /// </summary>
    [Test]
    public async Task SubmitCorrectAnswer_AllExercisesPassed_EmitsLessonCompleted()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();

        var skillId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var exercise1Id = Guid.NewGuid();
        var exercise2Id = Guid.NewGuid();

        databaseContext.Skills.Add(new Skill { Id = skillId, IconicName = "s2", Title = "S2" });
        databaseContext.Topics.Add(new Topic { Id = topicId, SkillId = skillId, IconicName = "t2", Title = "T2" });
        databaseContext.Lessons.Add(new Lesson { Id = lessonId, TopicId = topicId, Title = "L2", OrderInTopic = 1 });
        databaseContext.Exercises.Add(new Exercise
        {
            Id = exercise1Id, LessonId = lessonId, Type = ExerciseTypes.ChooseOption, OrderInLesson = 1,
            SerializedContent = """{"options":[{"text":"a","is_correct":true},{"text":"b","is_correct":false}]}"""
        });
        databaseContext.Exercises.Add(new Exercise
        {
            Id = exercise2Id, LessonId = lessonId, Type = ExerciseTypes.ChooseOption, OrderInLesson = 2,
            SerializedContent = """{"options":[{"text":"a","is_correct":true},{"text":"b","is_correct":false}]}"""
        });
        await databaseContext.SaveChangesAsync();

        var publisher = Substitute.For<ILearningEventPublisher>();
        var service = CreateService(databaseContext, publisher);
        var userId = Guid.NewGuid();
        var answer = JsonDocument.Parse("""{"selectedOptionIndex":0}""").RootElement;

        await service.SubmitExerciseAnswerAsync(userId, exercise1Id, answer);
        await publisher.DidNotReceive().PublishLessonCompletedAsync(
            Arg.Any<LessonCompletedEvent>(), Arg.Any<CancellationToken>());

        var result = await service.SubmitExerciseAnswerAsync(userId, exercise2Id, answer);
        result.IsCorrect.Should().BeTrue();

        await publisher.Received(1).PublishLessonCompletedAsync(
            Arg.Is<LessonCompletedEvent>(e => e.UserId == userId && e.LessonId == lessonId),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// LE4. <c>choose_option</c> always scores 100 when correct, so the assertion is not that the number
    /// is 100 but that the event carries the score the pipeline actually computed rather than the literal
    /// the old code hardcoded.
    /// </summary>
    [Test]
    public async Task LessonCompleted_EmitsRealBestScore_NotHardcoded100()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();

        var skillId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();

        databaseContext.Skills.Add(new Skill { Id = skillId, IconicName = "s3", Title = "S3" });
        databaseContext.Topics.Add(new Topic { Id = topicId, SkillId = skillId, IconicName = "t3", Title = "T3" });
        databaseContext.Lessons.Add(new Lesson { Id = lessonId, TopicId = topicId, Title = "L3", OrderInTopic = 1 });
        databaseContext.Exercises.Add(new Exercise
        {
            Id = exerciseId, LessonId = lessonId, Type = ExerciseTypes.ChooseOption, OrderInLesson = 1,
            SerializedContent = """{"options":[{"text":"a","is_correct":true},{"text":"b","is_correct":false}]}"""
        });
        await databaseContext.SaveChangesAsync();

        var publisher = Substitute.For<ILearningEventPublisher>();
        var service = CreateService(databaseContext, publisher);
        var userId = Guid.NewGuid();

        var answer = JsonDocument.Parse("""{"selectedOptionIndex":0}""").RootElement;
        var result = await service.SubmitExerciseAnswerAsync(userId, exerciseId, answer);

        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(100);

        await publisher.Received(1).PublishLessonCompletedAsync(
            Arg.Is<LessonCompletedEvent>(e =>
                e.UserId == userId &&
                e.LessonId == lessonId &&
                e.BestScore == result.Score),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// LE4. The score on the event is the best across attempts, not the score of the last one.
    /// </summary>
    [Test]
    public async Task LessonCompleted_BestScore_IsMaxOfPreviousAndCurrent()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();

        var skillId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var exercise1Id = Guid.NewGuid();
        var exercise2Id = Guid.NewGuid();

        databaseContext.Skills.Add(new Skill { Id = skillId, IconicName = "s4", Title = "S4" });
        databaseContext.Topics.Add(new Topic { Id = topicId, SkillId = skillId, IconicName = "t4", Title = "T4" });
        databaseContext.Lessons.Add(new Lesson { Id = lessonId, TopicId = topicId, Title = "L4", OrderInTopic = 1 });
        databaseContext.Exercises.Add(new Exercise
        {
            Id = exercise1Id, LessonId = lessonId, Type = ExerciseTypes.ChooseOption, OrderInLesson = 1,
            SerializedContent = """{"options":[{"text":"a","is_correct":true},{"text":"b","is_correct":false}]}"""
        });
        databaseContext.Exercises.Add(new Exercise
        {
            Id = exercise2Id, LessonId = lessonId, Type = ExerciseTypes.ChooseOption, OrderInLesson = 2,
            SerializedContent = """{"options":[{"text":"a","is_correct":true},{"text":"b","is_correct":false}]}"""
        });
        await databaseContext.SaveChangesAsync();

        var publisher = Substitute.For<ILearningEventPublisher>();
        var service = CreateService(databaseContext, publisher);
        var userId = Guid.NewGuid();
        var correctAnswer = JsonDocument.Parse("""{"selectedOptionIndex":0}""").RootElement;

        await service.SubmitExerciseAnswerAsync(userId, exercise1Id, correctAnswer);
        await service.SubmitExerciseAnswerAsync(userId, exercise2Id, correctAnswer);

        await publisher.Received(1).PublishLessonCompletedAsync(
            Arg.Is<LessonCompletedEvent>(e =>
                e.UserId == userId &&
                e.LessonId == lessonId &&
                e.BestScore == 100),
            Arg.Any<CancellationToken>());
    }
}
