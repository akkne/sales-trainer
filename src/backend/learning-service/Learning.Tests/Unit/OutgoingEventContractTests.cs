using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.Learning.Eventing;

namespace Sellevate.Learning.Tests.Unit;

[TestFixture]
public sealed class OutgoingEventContractTests
{
    [Test]
    public void ExerciseCompletedPayload_MatchesGamificationConsumerShape()
    {
        var userId = Guid.NewGuid();
        var payload = new ExerciseCompletedEvent(userId, "choose_option", 100, true);

        var json = JsonSerializer.Serialize(payload, EventEnvelope.JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("userId").GetGuid().Should().Be(userId);
        root.GetProperty("exerciseType").GetString().Should().Be("choose_option");
        root.GetProperty("score").GetInt32().Should().Be(100);
        root.GetProperty("isCorrect").GetBoolean().Should().BeTrue();
    }

    [Test]
    public void LessonCompletedPayload_MatchesGamificationConsumerShape()
    {
        var userId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var payload = new LessonCompletedEvent(userId, lessonId, 100);

        var json = JsonSerializer.Serialize(payload, EventEnvelope.JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("userId").GetGuid().Should().Be(userId);
        root.GetProperty("lessonId").GetGuid().Should().Be(lessonId);
        root.GetProperty("bestScore").GetInt32().Should().Be(100);
    }

    [Test]
    public void SkillCompletedPayload_MatchesGamificationConsumerShape()
    {
        var userId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var payload = new SkillCompletedEvent(userId, skillId);

        var json = JsonSerializer.Serialize(payload, EventEnvelope.JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("userId").GetGuid().Should().Be(userId);
        root.GetProperty("skillId").GetGuid().Should().Be(skillId);
    }

    [Test]
    public void TechniqueMasteryChangedPayload_ExposesCatalogueFields()
    {
        var userId = Guid.NewGuid();
        var techniqueId = Guid.NewGuid();
        var payload = new TechniqueMasteryChangedEvent(userId, techniqueId, 2, 60);

        var json = JsonSerializer.Serialize(payload, EventEnvelope.JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("userId").GetGuid().Should().Be(userId);
        root.GetProperty("techniqueId").GetGuid().Should().Be(techniqueId);
        root.GetProperty("level").GetInt32().Should().Be(2);
        root.GetProperty("masteryPercent").GetInt32().Should().Be(60);
    }

    [Test]
    public void ExerciseCompletedEnvelope_UsesTheCanonicalTopicName()
    {
        Topics.ExerciseCompleted.Should().Be("exercise.completed");
        Topics.LessonCompleted.Should().Be("lesson.completed");
        Topics.SkillCompleted.Should().Be("skill.completed");
        Topics.TechniqueMasteryChanged.Should().Be("technique.mastery.changed");
    }
}
