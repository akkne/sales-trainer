using FluentAssertions;
using NUnit.Framework;
using Sellevate.Analytics.Features.Funnels.Models;
using Sellevate.Analytics.Features.Funnels.Services.Implementation;
using Sellevate.Analytics.Infrastructure.Metrics;
using Sellevate.BuildingBlocks.Eventing;

namespace Sellevate.Analytics.Tests.Unit;

[TestFixture]
public class FunnelEventRecorderTests
{
    [Test]
    public void Record_UserRegistered_IncrementsRegistrationsCounter()
    {
        var recorder = new FunnelEventRecorder();
        var before = AppMetrics.Registrations.Value;
        var envelope = EventEnvelope.Create(
            Topics.UserRegistered,
            new UserRegisteredEvent(Guid.NewGuid(), "person@example.com", "Person", null));

        var wasRecorded = recorder.Record(envelope);

        wasRecorded.Should().BeTrue();
        AppMetrics.Registrations.Value.Should().Be(before + 1);
    }

    [Test]
    public void Record_ExerciseCompleted_IncrementsExercisesCompletedCounter()
    {
        var recorder = new FunnelEventRecorder();
        var before = AppMetrics.ExercisesCompleted.Value;
        var envelope = EventEnvelope.Create(
            Topics.ExerciseCompleted,
            new ExerciseCompletedEvent(Guid.NewGuid(), Guid.NewGuid()));

        var wasRecorded = recorder.Record(envelope);

        wasRecorded.Should().BeTrue();
        AppMetrics.ExercisesCompleted.Value.Should().Be(before + 1);
    }

    [Test]
    public void Record_ExperiencePointsGranted_IncrementsCounterByAmount()
    {
        var recorder = new FunnelEventRecorder();
        var before = AppMetrics.ExperiencePointsGranted.Value;
        var envelope = EventEnvelope.Create(
            Topics.XpGranted,
            new ExperiencePointsGrantedEvent(Guid.NewGuid(), 42));

        var wasRecorded = recorder.Record(envelope);

        wasRecorded.Should().BeTrue();
        AppMetrics.ExperiencePointsGranted.Value.Should().Be(before + 42);
    }

    [Test]
    public void Record_UnrelatedEventType_ReturnsFalseAndDoesNotThrow()
    {
        var recorder = new FunnelEventRecorder();
        var envelope = EventEnvelope.Create(Topics.DialogEvaluated, new { irrelevant = true });

        var wasRecorded = recorder.Record(envelope);

        wasRecorded.Should().BeFalse();
    }
}
