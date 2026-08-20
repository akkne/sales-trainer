using FluentAssertions;
using NUnit.Framework;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Features.SkillTree.Services.Implementation;

namespace Sellevate.Learning.Tests.Unit;

/// <summary>
/// The profile screen's headline numbers. It used to read them from identity-service, which answers
/// hard-coded zeros, and so reported 0% accuracy to learners averaging 94%. These tests pin the
/// numbers to the learner's actual rows, and pin the two definitions the screens must agree on:
/// accuracy is the mean best score over completed lessons, and the skill counts cover only the
/// skills the learner is enrolled in.
/// </summary>
[TestFixture]
public sealed class SkillTreeProgressSummaryTests
{
    [Test]
    public async Task ProgressSummary_AveragesBestScoreOverCompletedLessons()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();

        var skillId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        databaseContext.Skills.Add(new Skill { Id = skillId, IconicName = "s", Title = "S", OrderInTree = 1 });
        databaseContext.Topics.Add(new Topic { Id = topicId, SkillId = skillId, IconicName = "t", Title = "T" });

        var userId = Guid.NewGuid();
        var scores = new[] { 100, 90, 70 };
        for (var index = 0; index < scores.Length; index++)
        {
            var lessonId = Guid.NewGuid();
            databaseContext.Lessons.Add(new Lesson
            {
                Id = lessonId, TopicId = topicId, Title = $"L{index}", OrderInTopic = index + 1,
            });
            databaseContext.UserLessonProgressRecords.Add(new UserLessonProgress
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LessonId = lessonId,
                Status = LessonProgressStatuses.Completed,
                BestScore = scores[index],
            });
        }

        // A fourth lesson started but not finished must not drag the average down.
        var startedLessonId = Guid.NewGuid();
        databaseContext.Lessons.Add(new Lesson
        {
            Id = startedLessonId, TopicId = topicId, Title = "L3", OrderInTopic = 4,
        });
        databaseContext.UserLessonProgressRecords.Add(new UserLessonProgress
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LessonId = startedLessonId,
            Status = LessonProgressStatuses.InProgress,
            BestScore = 0,
        });

        await databaseContext.SaveChangesAsync();

        var summary = await new SkillTreeService(databaseContext).GetProgressSummaryForUserAsync(userId);

        summary.AverageExerciseScore.Should().Be(87); // (100 + 90 + 70) / 3 = 86.67
        summary.CompletedLessonCount.Should().Be(3);
    }

    /// <summary>
    /// "No lessons finished" and "finished them all with a score of zero" are different answers, and
    /// the profile screen renders them differently ("—" versus "0%").
    /// </summary>
    [Test]
    public async Task ProgressSummary_NothingCompleted_ReportsNullAccuracy()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();

        databaseContext.Skills.Add(new Skill { Id = Guid.NewGuid(), IconicName = "s", Title = "S", OrderInTree = 1 });
        await databaseContext.SaveChangesAsync();

        var summary = await new SkillTreeService(databaseContext)
            .GetProgressSummaryForUserAsync(Guid.NewGuid());

        summary.AverageExerciseScore.Should().BeNull();
        summary.CompletedLessonCount.Should().Be(0);
    }

    /// <summary>
    /// The skill counts must match what the tree shows the learner, so "1 of 2 skills" means the same
    /// thing on both screens. Skills they are not enrolled in are not part of their denominator.
    /// </summary>
    [Test]
    public async Task ProgressSummary_CountsOnlyEnrolledSkills()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();

        var enrolledSkillId = Guid.NewGuid();
        var otherSkillId = Guid.NewGuid();
        var enrolledTopicId = Guid.NewGuid();
        var enrolledLessonId = Guid.NewGuid();
        var otherTopicId = Guid.NewGuid();

        databaseContext.Skills.Add(new Skill
        {
            Id = enrolledSkillId, IconicName = "objections", Title = "Objections", OrderInTree = 1,
        });
        databaseContext.Skills.Add(new Skill
        {
            Id = otherSkillId, IconicName = "closing", Title = "Closing", OrderInTree = 2,
        });
        databaseContext.Topics.Add(new Topic
        {
            Id = enrolledTopicId, SkillId = enrolledSkillId, IconicName = "t1", Title = "T1",
        });
        databaseContext.Topics.Add(new Topic
        {
            Id = otherTopicId, SkillId = otherSkillId, IconicName = "t2", Title = "T2",
        });
        databaseContext.Lessons.Add(new Lesson
        {
            Id = enrolledLessonId, TopicId = enrolledTopicId, Title = "L", OrderInTopic = 1,
        });
        databaseContext.Lessons.Add(new Lesson
        {
            Id = Guid.NewGuid(), TopicId = otherTopicId, Title = "L2", OrderInTopic = 1,
        });
        await databaseContext.SaveChangesAsync();

        var userId = Guid.NewGuid();
        var service = new SkillTreeService(databaseContext);
        await service.UpdateEnrolledSkillsAsync(userId, new[] { "objections" });

        databaseContext.UserLessonProgressRecords.Add(new UserLessonProgress
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LessonId = enrolledLessonId,
            Status = LessonProgressStatuses.Completed,
            BestScore = 80,
        });
        await databaseContext.SaveChangesAsync();

        var summary = await service.GetProgressSummaryForUserAsync(userId);

        summary.TotalSkillCount.Should().Be(1);
        summary.CompletedSkillCount.Should().Be(1);
        summary.AverageExerciseScore.Should().Be(80);
    }
}
