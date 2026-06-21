using FluentAssertions;
using NUnit.Framework;
using Sellevate.Analytics.Features.Funnels.Models;
using Sellevate.Analytics.Features.Funnels.Services.Implementation;
using Sellevate.Analytics.Features.Tracking.Constants;
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
            new ExerciseCompletedEvent(Guid.NewGuid(), "spot_mistake", 80, true));

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
            new ExperiencePointsGrantedEvent(Guid.NewGuid(), 42, "exercise"));

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

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-1000)]
    public void Record_XpGrantedWithNonPositiveAmount_ReturnsFalseWithoutThrowingOrIncrementingCounter(int amount)
    {
        // A negative/zero amount must be silently ignored — not forwarded to Counter.Inc()
        // which would throw and send the Kafka message to the DLQ.
        var recorder = new FunnelEventRecorder();
        var before = AppMetrics.ExperiencePointsGranted.Value;
        var envelope = EventEnvelope.Create(
            Topics.XpGranted,
            new ExperiencePointsGrantedEvent(Guid.NewGuid(), amount, "exercise"));

        var act = () => recorder.Record(envelope);

        act.Should().NotThrow();
        var wasRecorded = act();
        wasRecorded.Should().BeFalse();
        AppMetrics.ExperiencePointsGranted.Value.Should().Be(before);
    }

    // AN3(c) — keep Prometheus label cardinality bounded.
    // These sets map directly to label values in Prometheus counters; unbounded growth
    // causes memory pressure and query fan-out. The cap is intentionally generous (< 50)
    // to allow natural growth while catching accidental bulk additions.
    private const int MaxAllowedLabelCardinality = 50;

    [Test]
    public void TrackedEvents_EventsSet_IsUnderCardinalityCap()
    {
        TrackedEvents.Events.Count.Should().BeLessThan(MaxAllowedLabelCardinality,
            because: "app_events_total uses 'event' as a Prometheus label; " +
                     "unbounded label cardinality causes memory pressure and slow queries");
    }

    [Test]
    public void TrackedEvents_PagesSet_IsUnderCardinalityCap()
    {
        TrackedEvents.Pages.Count.Should().BeLessThan(MaxAllowedLabelCardinality,
            because: "app_page_views_total and app_events_total use 'page' as a Prometheus label; " +
                     "unbounded label cardinality causes memory pressure and slow queries");
    }
}
