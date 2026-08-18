using FluentAssertions;
using NUnit.Framework;
using Sellevate.Analytics.Features.Funnels.Constants;
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

    /// <summary>
    /// A negative or zero amount must be silently ignored — not forwarded to <c>Counter.Inc()</c>,
    /// which would throw and send the Kafka message to the dead-letter queue.
    /// </summary>
    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(-1000)]
    public void Record_XpGrantedWithNonPositiveAmount_ReturnsFalseWithoutThrowingOrIncrementingCounter(int amount)
    {
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

    /// <summary>
    /// Phase 40.25. An unrecognised assignment status is <b>dropped, never counted</b>.
    ///
    /// <para>
    /// The four values are copied from learning-service's <c>AssignmentProgressStatuses</c> rather than
    /// referenced: analytics-service does not depend on learning-service and must not start to for a
    /// list of four strings. Copied lists drift, and the rule <c>docs/ANALYTICS_SERVICE.md</c> sets for
    /// that case is the one under test here — folding an unknown value into a known bucket makes the
    /// number unverifiable, so an unknown status must not increment anything at all. A label it does not
    /// recognise would also be new Prometheus cardinality arriving from another service's deploy.
    /// </para>
    /// </summary>
    [TestCase("in_reviewww")]
    [TestCase("NOT_STARTED")]
    [TestCase("completed ")]
    [TestCase("archived")]
    public void Record_AssignmentProgressWithUnknownStatus_ReturnsFalseWithoutCountingIt(string status)
    {
        var recorder = new FunnelEventRecorder();
        var envelope = EventEnvelope.Create(
            Topics.AssignmentProgressChanged,
            new AssignmentProgressChangedEvent(
                Guid.NewGuid(), Guid.NewGuid(), "not_started", status, BestScore: null, AttemptCount: 1));

        var wasRecorded = recorder.Record(envelope);

        wasRecorded.Should().BeFalse();
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Record_AssignmentProgressWithBlankStatus_ReturnsFalse(string status)
    {
        var recorder = new FunnelEventRecorder();
        var envelope = EventEnvelope.Create(
            Topics.AssignmentProgressChanged,
            new AssignmentProgressChangedEvent(
                Guid.NewGuid(), Guid.NewGuid(), "not_started", status, BestScore: null, AttemptCount: 1));

        recorder.Record(envelope).Should().BeFalse();
    }

    /// <summary>
    /// The mirror of the rule above: each of the four known statuses is counted, under its own label.
    /// Without this the guard could be tightened into refusing everything and no test would notice.
    /// </summary>
    [TestCase("not_started")]
    [TestCase("in_progress")]
    [TestCase("completed")]
    [TestCase("failed_threshold")]
    public void Record_AssignmentProgressWithKnownStatus_CountsItUnderThatStatusLabel(string status)
    {
        var recorder = new FunnelEventRecorder();
        var before = AppMetrics.AssignmentProgressTransitions.WithLabels(status).Value;
        var envelope = EventEnvelope.Create(
            Topics.AssignmentProgressChanged,
            new AssignmentProgressChangedEvent(
                Guid.NewGuid(), Guid.NewGuid(), "not_started", status, BestScore: 90, AttemptCount: 2));

        var wasRecorded = recorder.Record(envelope);

        wasRecorded.Should().BeTrue();
        AppMetrics.AssignmentProgressTransitions.WithLabels(status).Value.Should().Be(before + 1);
    }

    /// <summary>
    /// The copied list must stay exactly the four values learning-service publishes. If it ever gains a
    /// fifth locally, the funnel starts counting a status the producer never sends; if it loses one, a
    /// real transition stops being counted and the drop is silent by design.
    /// </summary>
    [Test]
    public void The_copied_status_vocabulary_is_exactly_the_four_learning_service_publishes()
    {
        AssignmentProgressStatuses.Known.Should().BeEquivalentTo(
            ["not_started", "in_progress", "completed", "failed_threshold"]);
    }

    /// <summary>
    /// AN3(c) — keep Prometheus label cardinality bounded. The tracked-event sets map directly to
    /// label values in Prometheus counters; unbounded growth causes memory pressure and query
    /// fan-out. The cap is intentionally generous to allow natural growth while catching accidental
    /// bulk additions.
    /// </summary>
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
