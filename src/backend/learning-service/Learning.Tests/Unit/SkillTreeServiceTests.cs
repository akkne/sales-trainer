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
    public async Task UpdateEnrolledSkills_MarksUnenrolledSkillsLocked_AndKeepsCoreEnrolled()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();

        var coreId = Guid.NewGuid();
        var wantedId = Guid.NewGuid();
        var droppedId = Guid.NewGuid();
        databaseContext.Skills.Add(new Skill { Id = coreId, IconicName = "sales-basics", Title = "Core", OrderInTree = 1 });
        databaseContext.Skills.Add(new Skill { Id = wantedId, IconicName = "objections", Title = "Objections", OrderInTree = 2 });
        databaseContext.Skills.Add(new Skill { Id = droppedId, IconicName = "closing", Title = "Closing", OrderInTree = 3 });
        await databaseContext.SaveChangesAsync();

        var userId = Guid.NewGuid();
        var service = new SkillTreeService(databaseContext);

        // Enroll only "objections"; core is kept automatically, "closing" stays out.
        await service.UpdateEnrolledSkillsAsync(userId, new[] { "objections" });

        var skills = await service.GetAllSkillsWithProgressAsync(userId);
        var bySlug = skills.ToDictionary(skill => skill.Slug);

        bySlug["sales-basics"].Status.Should().NotBe(LessonProgressStatuses.Locked);
        bySlug["sales-basics"].IsLocked.Should().BeFalse();
        bySlug["objections"].Status.Should().NotBe(LessonProgressStatuses.Locked);
        bySlug["objections"].IsLocked.Should().BeFalse();
        bySlug["closing"].Status.Should().Be(LessonProgressStatuses.Locked);
        bySlug["closing"].IsLocked.Should().BeTrue();
    }

    [Test]
    public async Task UpdateEnrolledSkills_ReplacesPreviousSet()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();

        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        databaseContext.Skills.Add(new Skill { Id = aId, IconicName = "a", Title = "A", OrderInTree = 1 });
        databaseContext.Skills.Add(new Skill { Id = bId, IconicName = "b", Title = "B", OrderInTree = 2 });
        await databaseContext.SaveChangesAsync();

        var userId = Guid.NewGuid();
        var service = new SkillTreeService(databaseContext);

        await service.UpdateEnrolledSkillsAsync(userId, new[] { "a" });
        await service.UpdateEnrolledSkillsAsync(userId, new[] { "b" });

        var skills = await service.GetAllSkillsWithProgressAsync(userId);
        var bySlug = skills.ToDictionary(skill => skill.Slug);

        bySlug["a"].Status.Should().Be(LessonProgressStatuses.Locked);
        bySlug["b"].Status.Should().NotBe(LessonProgressStatuses.Locked);
    }

    [Test]
    public async Task GetAllSkillsWithProgress_NoEnrollmentRows_TreatsAllAsEnrolled()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();
        databaseContext.Skills.Add(new Skill { Id = Guid.NewGuid(), IconicName = "x", Title = "X", OrderInTree = 1 });
        databaseContext.Skills.Add(new Skill { Id = Guid.NewGuid(), IconicName = "y", Title = "Y", OrderInTree = 2 });
        await databaseContext.SaveChangesAsync();

        var service = new SkillTreeService(databaseContext);

        var skills = await service.GetAllSkillsWithProgressAsync(Guid.NewGuid());

        skills.Should().OnlyContain(skill => skill.Status != LessonProgressStatuses.Locked);
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
