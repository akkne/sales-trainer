using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Sellevate.Learning.Features.Admin;
using Sellevate.Learning.Features.Lessons.Models;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Tests.Unit;

[TestFixture]
public sealed class AdminSeederExportTests
{
    private static AdminSeederController CreateController(LearningDbContext databaseContext) =>
        new(databaseContext, NullLogger<AdminSeederController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    private static async Task<LearningDbContext> SeedTreeAsync()
    {
        var databaseContext = LearningDbContextFactory.CreateInMemory();
        var skillId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();

        databaseContext.Skills.Add(new Skill
        {
            Id = skillId, IconicName = "cold-calling", Title = "Cold Calling",
            Description = "desc", OrderInTree = 1, Stage = "preparation",
        });
        databaseContext.Topics.Add(new Topic
        {
            Id = topicId, SkillId = skillId, IconicName = "cc-basics", Title = "Basics", OrderInSkill = 1,
        });
        databaseContext.Lessons.Add(new Lesson
        {
            Id = lessonId, TopicId = topicId, Title = "Opening the call", OrderInTopic = 1,
        });
        databaseContext.Exercises.Add(new Exercise
        {
            Id = Guid.NewGuid(), LessonId = lessonId, Type = "choose_option", OrderInLesson = 1,
            SerializedContent = """{"layout":"text","title":"Hi"}""", CustomAiPrompt = null,
        });
        await databaseContext.SaveChangesAsync();
        return databaseContext;
    }

    [Test]
    public async Task ExportSkills_ReturnsImportableShape()
    {
        await using var databaseContext = await SeedTreeAsync();

        var result = await CreateController(databaseContext).ExportSkills();

        var payload = (result.Result as OkObjectResult)!.Value as IReadOnlyList<SkillExportDto>;
        payload.Should().ContainSingle();
        payload![0].IconicName.Should().Be("cold-calling");
        payload[0].Stage.Should().Be("preparation");
        payload[0].OrderInTree.Should().Be(1);
    }

    [Test]
    public async Task ExportTopics_ResolvesSkillIconicName()
    {
        await using var databaseContext = await SeedTreeAsync();

        var result = await CreateController(databaseContext).ExportTopics();

        var payload = (result.Result as OkObjectResult)!.Value as IReadOnlyList<TopicExportDto>;
        payload.Should().ContainSingle();
        payload![0].SkillIconicName.Should().Be("cold-calling");
        payload[0].IconicName.Should().Be("cc-basics");
        payload[0].OrderInSkill.Should().Be(1);
    }

    /// <summary>
    /// Exercise content is emitted as a JSON object, not as a string: the seeder import reads it back as a
    /// document, so a quoted string would round-trip into double-encoded content.
    /// </summary>
    [Test]
    public async Task ExportLessons_ResolvesTopicIconicName_AndNestsExercises()
    {
        await using var databaseContext = await SeedTreeAsync();

        var result = await CreateController(databaseContext).ExportLessons();

        var payload = (result.Result as OkObjectResult)!.Value as IReadOnlyList<LessonExportDto>;
        payload.Should().ContainSingle();
        payload![0].TopicIconicName.Should().Be("cc-basics");
        payload[0].Exercises.Should().ContainSingle();
        var exercise = payload[0].Exercises[0];
        exercise.Type.Should().Be("choose_option");
        exercise.Content.Should().NotBeNull();
        exercise.Content!["layout"]!.GetValue<string>().Should().Be("text");
    }

    [Test]
    public async Task ExportBundle_NestsSkillTopicLessonExercise()
    {
        await using var databaseContext = await SeedTreeAsync();

        var result = await CreateController(databaseContext).ExportBundle();

        var bundle = (result.Result as OkObjectResult)!.Value as BundleExportDto;
        bundle.Should().NotBeNull();
        var skill = bundle!.Skills.Should().ContainSingle().Subject;
        skill.IconicName.Should().Be("cold-calling");
        var topic = skill.Topics.Should().ContainSingle().Subject;
        topic.IconicName.Should().Be("cc-basics");
        var lesson = topic.Lessons.Should().ContainSingle().Subject;
        lesson.Title.Should().Be("Opening the call");
        lesson.Exercises.Should().ContainSingle();
        lesson.Exercises[0].Content.Should().NotBeNull();
    }
}
