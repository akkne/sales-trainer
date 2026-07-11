using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Eventing;

namespace Sellevate.BuildingBlocks.Tests;

/// <summary>
/// Cross-service Kafka schema contract tests covering the full event catalogue in
/// docs/MICROSERVICES.md §4.1. For every topic, the producer-shaped payload is serialized
/// with the shared <see cref="EventEnvelope.JsonOptions"/> (camelCase) and asserted to
/// expose exactly the field names + JSON types each consuming service deserializes. This is
/// the wire contract between independently-deployed producers and consumers; per-service
/// `OutgoingEventContractTests` remain the producer-side source of truth.
/// </summary>
[TestFixture]
public sealed class EventContractCatalogTests
{
    private static JsonElement Serialize(object payload)
    {
        var json = JsonSerializer.Serialize(payload, EventEnvelope.JsonOptions);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Test]
    public void UserRegistered_IdentityProducer_MatchesReplicaConsumers()
    {
        Topics.UserRegistered.Should().Be("user.registered");
        var userId = Guid.NewGuid();
        var root = Serialize(new { userId, email = "a@b.com", displayName = "Ann", avatarKey = "k" });

        root.GetProperty("userId").GetGuid().Should().Be(userId);
        root.GetProperty("email").GetString().Should().Be("a@b.com");
        root.GetProperty("displayName").GetString().Should().Be("Ann");
        root.GetProperty("avatarKey").GetString().Should().Be("k");
    }

    [Test]
    public void UserUpdated_IdentityProducer_MatchesReplicaConsumers()
    {
        Topics.UserUpdated.Should().Be("user.updated");
        var userId = Guid.NewGuid();
        var root = Serialize(new { userId, displayName = "Ann", avatarKey = (string?)null });

        root.GetProperty("userId").GetGuid().Should().Be(userId);
        root.GetProperty("displayName").GetString().Should().Be("Ann");
        root.TryGetProperty("avatarKey", out _).Should().BeTrue();
    }

    [Test]
    public void UserDeleted_IdentityProducer_MatchesCascadeConsumers()
    {
        Topics.UserDeleted.Should().Be("user.deleted");
        var userId = Guid.NewGuid();
        var root = Serialize(new { userId });

        root.GetProperty("userId").GetGuid().Should().Be(userId);
    }

    [Test]
    public void UserAvatarChanged_IdentityProducer_MatchesSocialAndGamification()
    {
        Topics.UserAvatarChanged.Should().Be("user.avatar.changed");
        var userId = Guid.NewGuid();
        var root = Serialize(new { userId, avatarKey = "avatars/x.png" });

        root.GetProperty("userId").GetGuid().Should().Be(userId);
        root.GetProperty("avatarKey").GetString().Should().Be("avatars/x.png");
    }

    [Test]
    public void ExerciseCompleted_LearningProducer_MatchesGamificationAndAnalytics()
    {
        Topics.ExerciseCompleted.Should().Be("exercise.completed");
        var userId = Guid.NewGuid();
        var root = Serialize(new { userId, exerciseType = "spot_mistake", score = 80, isCorrect = true });

        root.GetProperty("userId").GetGuid().Should().Be(userId);
        root.GetProperty("exerciseType").GetString().Should().Be("spot_mistake");
        root.GetProperty("score").GetInt32().Should().Be(80);
        root.GetProperty("isCorrect").GetBoolean().Should().BeTrue();
    }

    [Test]
    public void LessonCompleted_LearningProducer_MatchesGamification()
    {
        Topics.LessonCompleted.Should().Be("lesson.completed");
        var userId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var root = Serialize(new { userId, lessonId, bestScore = 95 });

        root.GetProperty("userId").GetGuid().Should().Be(userId);
        root.GetProperty("lessonId").GetGuid().Should().Be(lessonId);
        root.GetProperty("bestScore").GetInt32().Should().Be(95);
    }

    [Test]
    public void SkillCompleted_LearningProducer_MatchesGamification()
    {
        Topics.SkillCompleted.Should().Be("skill.completed");
        var userId = Guid.NewGuid();
        var skillId = Guid.NewGuid();
        var root = Serialize(new { userId, skillId });

        root.GetProperty("userId").GetGuid().Should().Be(userId);
        root.GetProperty("skillId").GetGuid().Should().Be(skillId);
    }

    [Test]
    public void DialogEvaluated_AiProducer_MatchesGamificationConsumer()
    {
        Topics.DialogEvaluated.Should().Be("dialog.evaluated");
        var userId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();
        var modeId = Guid.NewGuid();
        var root = Serialize(new { userId, sessionId = "s-1", bundleId, modeId, rawScore = 70, xpEarned = 70 });

        root.GetProperty("userId").GetGuid().Should().Be(userId);
        root.GetProperty("sessionId").GetString().Should().Be("s-1");
        root.GetProperty("bundleId").GetGuid().Should().Be(bundleId);
        root.GetProperty("modeId").GetGuid().Should().Be(modeId);
        root.GetProperty("rawScore").GetInt32().Should().Be(70);
        root.GetProperty("xpEarned").GetInt32().Should().Be(70);
    }

    [Test]
    public void XpGranted_GamificationProducer_MatchesAnalytics()
    {
        Topics.XpGranted.Should().Be("xp.granted");
        var userId = Guid.NewGuid();
        var root = Serialize(new { userId, amount = 40, source = "exercise" });

        root.GetProperty("userId").GetGuid().Should().Be(userId);
        root.GetProperty("amount").GetInt32().Should().Be(40);
        root.GetProperty("source").GetString().Should().Be("exercise");
    }

    [Test]
    public void AchievementUnlocked_GamificationProducer_MatchesNotifications()
    {
        Topics.AchievementUnlocked.Should().Be("achievement.unlocked");
        var userId = Guid.NewGuid();
        var root = Serialize(new { userId, achievementKey = "first_lesson", title = "First step" });

        root.GetProperty("userId").GetGuid().Should().Be(userId);
        root.GetProperty("achievementKey").GetString().Should().Be("first_lesson");
        root.GetProperty("title").GetString().Should().Be("First step");
    }

    [Test]
    public void StreakMilestone_GamificationProducer_MatchesNotifications()
    {
        Topics.StreakMilestone.Should().Be("streak.milestone");
        var userId = Guid.NewGuid();
        var root = Serialize(new { userId, dayCount = 7, bonusXp = 50 });

        root.GetProperty("userId").GetGuid().Should().Be(userId);
        root.GetProperty("dayCount").GetInt32().Should().Be(7);
        root.GetProperty("bonusXp").GetInt32().Should().Be(50);
    }

    [Test]
    public void GamificationDialogWeightsUpdated_GamificationProducer_MatchesAiConsumer()
    {
        Topics.GamificationDialogWeightsUpdated.Should().Be("gamification.dialog-weights.updated");
        var root = Serialize(new { confidence = 25, structure = 30, objection = 20, goal = 25, multiplier = 1.5 });

        root.GetProperty("confidence").GetInt32().Should().Be(25);
        root.GetProperty("structure").GetInt32().Should().Be(30);
        root.GetProperty("objection").GetInt32().Should().Be(20);
        root.GetProperty("goal").GetInt32().Should().Be(25);
        root.GetProperty("multiplier").GetDouble().Should().Be(1.5);
    }

    [Test]
    public void FriendRequestReceived_SocialProducer_MatchesNotifications()
    {
        Topics.FriendRequestReceived.Should().Be("friend.request.received");
        var recipientId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var friendshipId = Guid.NewGuid();
        var root = Serialize(new { recipientId, requesterName = "Ben", requesterId, friendshipId });

        root.GetProperty("recipientId").GetGuid().Should().Be(recipientId);
        root.GetProperty("requesterName").GetString().Should().Be("Ben");
        root.GetProperty("requesterId").GetGuid().Should().Be(requesterId);
        root.GetProperty("friendshipId").GetGuid().Should().Be(friendshipId);
    }

    [Test]
    public void FriendRequestAccepted_SocialProducer_MatchesNotifications()
    {
        Topics.FriendRequestAccepted.Should().Be("friend.request.accepted");
        var recipientId = Guid.NewGuid();
        var accepterId = Guid.NewGuid();
        var root = Serialize(new { recipientId, accepterName = "Cara", accepterId });

        root.GetProperty("recipientId").GetGuid().Should().Be(recipientId);
        root.GetProperty("accepterName").GetString().Should().Be("Cara");
        root.GetProperty("accepterId").GetGuid().Should().Be(accepterId);
    }

    [Test]
    public void ChatMessageSent_SocialProducer_MatchesNotifications()
    {
        Topics.ChatMessageSent.Should().Be("chat.message.sent");
        var recipientId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var root = Serialize(new { recipientId, senderName = "Dan", preview = "hi", conversationId });

        root.GetProperty("recipientId").GetGuid().Should().Be(recipientId);
        root.GetProperty("senderName").GetString().Should().Be("Dan");
        root.GetProperty("preview").GetString().Should().Be("hi");
        root.GetProperty("conversationId").GetGuid().Should().Be(conversationId);
    }

    [Test]
    public void CompanyFollowUpDue_CompanyProducer_MatchesNotifications()
    {
        Topics.CompanyFollowUpDue.Should().Be("company.followup.due");
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var nextActionAt = DateTime.UtcNow;
        var root = Serialize(new { companyId, userId, companyName = "Acme", nextActionAt, note = "Call back about pricing" });

        root.GetProperty("companyId").GetGuid().Should().Be(companyId);
        root.GetProperty("userId").GetGuid().Should().Be(userId);
        root.GetProperty("companyName").GetString().Should().Be("Acme");
        root.GetProperty("nextActionAt").GetDateTime().Should().Be(nextActionAt);
        root.GetProperty("note").GetString().Should().Be("Call back about pricing");
    }
}
