using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
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
public sealed class LessonOrderingTests
{
    private static ExerciseService CreateService(LearningDbContext databaseContext)
    {
        var factory = new ExerciseEvaluationFactory(
            new IExerciseEvaluationStrategy[] { new TheoryCardEvaluationStrategy() },
            Substitute.For<IAiEvaluationClient>(),
            databaseContext,
            new StubOrganizationProfileProvider());

        return new ExerciseService(
            databaseContext,
            factory,
            Substitute.For<ILearningEventPublisher>(),
            Substitute.For<IExerciseDialogService>(),
            new LessonVersionService(databaseContext),
            new StubOrganizationProfileProvider(),
            NullLogger<ExerciseService>.Instance);
    }

    /// <summary>
    /// Topic one's lessons must come fully before topic two's, each internally ordered by position in its
    /// topic. The topics are seeded out of order on purpose, so the assertion proves the read sorts by the
    /// declared order rather than by insertion.
    /// </summary>
    [Test]
    public async Task GetLessonsForSkill_OrdersByTopicThenLesson_NotInterleaved()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();

        var skillId = Guid.NewGuid();
        var topicOne = Guid.NewGuid();
        var topicTwo = Guid.NewGuid();

        databaseContext.Skills.Add(new Skill { Id = skillId, IconicName = "cold-calling", Title = "Cold calling" });
        databaseContext.Topics.Add(new Topic { Id = topicTwo, SkillId = skillId, IconicName = "objections", Title = "Objections", OrderInSkill = 2 });
        databaseContext.Topics.Add(new Topic { Id = topicOne, SkillId = skillId, IconicName = "basics", Title = "Basics", OrderInSkill = 1 });

        databaseContext.Lessons.Add(new Lesson { Id = Guid.NewGuid(), TopicId = topicTwo, Title = "T2-L1", OrderInTopic = 1 });
        databaseContext.Lessons.Add(new Lesson { Id = Guid.NewGuid(), TopicId = topicOne, Title = "T1-L2", OrderInTopic = 2 });
        databaseContext.Lessons.Add(new Lesson { Id = Guid.NewGuid(), TopicId = topicOne, Title = "T1-L1", OrderInTopic = 1 });
        databaseContext.Lessons.Add(new Lesson { Id = Guid.NewGuid(), TopicId = topicTwo, Title = "T2-L2", OrderInTopic = 2 });
        await databaseContext.SaveChangesAsync();

        var service = CreateService(databaseContext);

        var lessons = await service.GetLessonsForSkillAsync(Guid.NewGuid(), "cold-calling");

        lessons.Select(lesson => lesson.Title).Should()
            .ContainInOrder("T1-L1", "T1-L2", "T2-L1", "T2-L2");
        lessons.Select(lesson => lesson.TopicOrder).Should()
            .ContainInOrder(1, 1, 2, 2);
    }
}
