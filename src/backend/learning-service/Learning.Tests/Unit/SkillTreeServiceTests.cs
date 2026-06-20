using FluentAssertions;
using NUnit.Framework;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Features.SkillTree.Services.Implementation;

namespace Sellevate.Learning.Tests.Unit;

[TestFixture]
public sealed class SkillTreeServiceTests
{
    [Test]
    public async Task GetAllSkillsWithProgress_AllLessonsCompleted_ReportsCompleted()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();

        var skillId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        databaseContext.Skills.Add(new Skill { Id = skillId, IconicName = "s", Title = "S", OrderInTree = 1 });
        databaseContext.Topics.Add(new Topic { Id = topicId, SkillId = skillId, IconicName = "t", Title = "T" });
        databaseContext.Lessons.Add(new Lesson { Id = lessonId, TopicId = topicId, Title = "L", OrderInTopic = 1 });

        var userId = Guid.NewGuid();
        databaseContext.UserLessonProgressRecords.Add(new UserLessonProgress
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LessonId = lessonId,
            Status = LessonProgressStatuses.Completed,
            BestScore = 100,
        });
        await databaseContext.SaveChangesAsync();

        var service = new SkillTreeService(databaseContext);

        var skills = await service.GetAllSkillsWithProgressAsync(userId);

        skills.Should().HaveCount(1);
        skills[0].Status.Should().Be(LessonProgressStatuses.Completed);
        skills[0].CompletedLessonCount.Should().Be(1);
        skills[0].TotalLessonCount.Should().Be(1);
    }

    [Test]
    public async Task GetSkillTreeForUser_ReturnsGamificationAggregatesAsZero()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();
        databaseContext.Skills.Add(new Skill { Id = Guid.NewGuid(), IconicName = "s", Title = "S" });
        await databaseContext.SaveChangesAsync();

        var service = new SkillTreeService(databaseContext);

        var response = await service.GetSkillTreeForUserAsync(Guid.NewGuid());

        response.TotalXpAmount.Should().Be(0);
        response.CurrentStreakDayCount.Should().Be(0);
        response.DailyXpGoal.Should().Be(0);
        response.SkillNodes.Should().HaveCount(1);
    }
}
