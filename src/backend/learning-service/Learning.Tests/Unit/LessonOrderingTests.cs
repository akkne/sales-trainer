using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Learning.Eventing;
using Sellevate.Learning.Features.Exercises.Services.Abstract;
using Sellevate.Learning.Features.Exercises.Services.Implementation;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Infrastructure.Ai;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Tests.Unit;

[TestFixture]
public sealed class LessonOrderingTests
{
    private static ExerciseService CreateService(LearningDbContext databaseContext)
    {
        var factory = new ExerciseEvaluationFactory(
            new IExerciseEvaluationStrategy[] { new TheoryCardEvaluationStrategy() },
            Substitute.For<IAiEvaluationClient>(),
            databaseContext);

        return new ExerciseService(
            databaseContext,
            factory,
            Substitute.For<ILearningEventPublisher>(),
            Substitute.For<IExerciseDialogService>());
    }

    [Test]
    public async Task GetLessonsForSkill_OrdersByTopicThenLesson_NotInterleaved()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();

        var skillId = Guid.NewGuid();
        var topicOne = Guid.NewGuid();
        var topicTwo = Guid.NewGuid();

        databaseContext.Skills.Add(new Skill { Id = skillId, IconicName = "cold-calling", Title = "Cold calling" });
        // Topic two is added first / out of order to prove we sort by OrderInSkill, not insertion.
        databaseContext.Topics.Add(new Topic { Id = topicTwo, SkillId = skillId, IconicName = "objections", Title = "Objections", OrderInSkill = 2 });
        databaseContext.Topics.Add(new Topic { Id = topicOne, SkillId = skillId, IconicName = "basics", Title = "Basics", OrderInSkill = 1 });

        databaseContext.Lessons.Add(new Lesson { Id = Guid.NewGuid(), TopicId = topicTwo, Title = "T2-L1", OrderInTopic = 1 });
        databaseContext.Lessons.Add(new Lesson { Id = Guid.NewGuid(), TopicId = topicOne, Title = "T1-L2", OrderInTopic = 2 });
        databaseContext.Lessons.Add(new Lesson { Id = Guid.NewGuid(), TopicId = topicOne, Title = "T1-L1", OrderInTopic = 1 });
        databaseContext.Lessons.Add(new Lesson { Id = Guid.NewGuid(), TopicId = topicTwo, Title = "T2-L2", OrderInTopic = 2 });
        await databaseContext.SaveChangesAsync();

        var service = CreateService(databaseContext);

        var lessons = await service.GetLessonsForSkillAsync(Guid.NewGuid(), "cold-calling");

        // Topic 1's lessons must come fully before Topic 2's, each internally ordered by OrderInTopic.
        lessons.Select(lesson => lesson.Title).Should()
            .ContainInOrder("T1-L1", "T1-L2", "T2-L1", "T2-L2");
        lessons.Select(lesson => lesson.TopicOrder).Should()
            .ContainInOrder(1, 1, 2, 2);
    }
}
