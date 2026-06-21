using FluentAssertions;
using NUnit.Framework;
using Sellevate.Analytics.Features.Funnels.Models;
using Sellevate.BuildingBlocks.Eventing;

namespace Sellevate.Analytics.Tests.Unit;

/// <summary>
/// Consumer-side contract tests. Unlike <see cref="FunnelEventRecorderTests"/> (which construct the
/// consumer record directly), these build the exact <b>producer</b> wire payload and deserialize it
/// through <see cref="EventEnvelope.DataAs{T}"/> into the consumer record this service actually uses,
/// asserting every field is populated (non-default). This is the test that fails when a producer and a
/// consumer drift apart — e.g. the historical `exercise.completed` mismatch where Learning emitted
/// {userId, exerciseType, score, isCorrect} but Analytics deserialized into {UserId, ExerciseId}.
/// </summary>
[TestFixture]
public sealed class IncomingEventContractTests
{
    [Test]
    public void ExerciseCompleted_LearningWirePayload_DeserializesWithAllFieldsPopulated()
    {
        var userId = Guid.NewGuid();
        // Exact producer wire shape (Learning/Eventing/OutgoingIntegrationEvents.cs).
        var envelope = EventEnvelope.Create(
            Topics.ExerciseCompleted,
            new { userId, exerciseType = "spot_mistake", score = 80, isCorrect = true });

        var payload = envelope.DataAs<ExerciseCompletedEvent>();

        payload.Should().NotBeNull();
        payload!.UserId.Should().Be(userId);
        payload.ExerciseType.Should().Be("spot_mistake");
        payload.Score.Should().Be(80);
        payload.IsCorrect.Should().BeTrue();
    }

    [Test]
    public void XpGranted_GamificationWirePayload_DeserializesWithAllFieldsPopulated()
    {
        var userId = Guid.NewGuid();
        // Exact producer wire shape (Gamification/Eventing/OutgoingIntegrationEvents.cs).
        var envelope = EventEnvelope.Create(
            Topics.XpGranted,
            new { userId, amount = 40, source = "exercise" });

        var payload = envelope.DataAs<ExperiencePointsGrantedEvent>();

        payload.Should().NotBeNull();
        payload!.UserId.Should().Be(userId);
        payload.Amount.Should().Be(40);
        payload.Source.Should().Be("exercise");
    }

    [Test]
    public void UserRegistered_IdentityWirePayload_DeserializesWithAllFieldsPopulated()
    {
        var userId = Guid.NewGuid();
        var envelope = EventEnvelope.Create(
            Topics.UserRegistered,
            new { userId, email = "person@example.com", displayName = "Person", avatarKey = "avatars/x.png" });

        var payload = envelope.DataAs<UserRegisteredEvent>();

        payload.Should().NotBeNull();
        payload!.UserId.Should().Be(userId);
        payload.Email.Should().Be("person@example.com");
        payload.DisplayName.Should().Be("Person");
        payload.AvatarKey.Should().Be("avatars/x.png");
    }
}
