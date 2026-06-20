using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.Gamification.Eventing;

namespace Sellevate.Gamification.Tests.Unit;

[TestFixture]
public sealed class OutgoingEventContractTests
{
    [Test]
    public void DialogWeightsUpdatedPayload_SerializesToTheShapeAiServiceExpects()
    {
        var payload = new GamificationDialogWeightsUpdatedEvent(25, 30, 20, 25, 1.5);

        var json = JsonSerializer.Serialize(payload, EventEnvelope.JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("confidence").GetInt32().Should().Be(25);
        root.GetProperty("structure").GetInt32().Should().Be(30);
        root.GetProperty("objection").GetInt32().Should().Be(20);
        root.GetProperty("goal").GetInt32().Should().Be(25);
        root.GetProperty("multiplier").GetDouble().Should().Be(1.5);
    }

    [Test]
    public void ExperiencePointsGrantedPayload_ExposesUserIdAndAmountForAnalytics()
    {
        var userId = Guid.NewGuid();
        var payload = new ExperiencePointsGrantedEvent(userId, 40, "exercise");

        var json = JsonSerializer.Serialize(payload, EventEnvelope.JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("userId").GetGuid().Should().Be(userId);
        root.GetProperty("amount").GetInt32().Should().Be(40);
        root.GetProperty("source").GetString().Should().Be("exercise");
    }

    [Test]
    public void AchievementUnlockedPayload_MatchesNotificationServiceContract()
    {
        var userId = Guid.NewGuid();
        var payload = new AchievementUnlockedEvent(userId, "first_lesson", "First step");

        var json = JsonSerializer.Serialize(payload, EventEnvelope.JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("userId").GetGuid().Should().Be(userId);
        root.GetProperty("achievementKey").GetString().Should().Be("first_lesson");
        root.GetProperty("title").GetString().Should().Be("First step");
    }

    [Test]
    public void StreakMilestonePayload_MatchesNotificationServiceContract()
    {
        var userId = Guid.NewGuid();
        var payload = new StreakMilestoneEvent(userId, 7, 50);

        var json = JsonSerializer.Serialize(payload, EventEnvelope.JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("userId").GetGuid().Should().Be(userId);
        root.GetProperty("dayCount").GetInt32().Should().Be(7);
        root.GetProperty("bonusXp").GetInt32().Should().Be(50);
    }

    [Test]
    public void DialogEvaluatedPayload_DeserializesFromTheShapeAiServiceProduces()
    {
        var userId = Guid.NewGuid();
        var aiPayload = new
        {
            userId,
            sessionId = "session-1",
            bundleId = Guid.NewGuid(),
            modeId = Guid.NewGuid(),
            rawScore = 80,
            xpEarned = 80,
        };
        var json = JsonSerializer.Serialize(aiPayload, EventEnvelope.JsonOptions);

        var parsed = JsonSerializer.Deserialize<DialogEvaluatedEvent>(json, EventEnvelope.JsonOptions);

        parsed.Should().NotBeNull();
        parsed!.UserId.Should().Be(userId);
        parsed.XpEarned.Should().Be(80);
        parsed.RawScore.Should().Be(80);
    }
}
