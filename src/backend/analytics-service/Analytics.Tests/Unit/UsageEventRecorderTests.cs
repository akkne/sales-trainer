using FluentAssertions;
using NUnit.Framework;
using Sellevate.Analytics.Features.Tracking.Constants;
using Sellevate.Analytics.Features.Tracking.Models;
using Sellevate.Analytics.Features.Tracking.Services.Implementation;
using Sellevate.Analytics.Infrastructure.Metrics;

namespace Sellevate.Analytics.Tests.Unit;

[TestFixture]
public class UsageEventRecorderTests
{
    [Test]
    public void TryRecord_WithUnknownEvent_ReturnsFalse()
    {
        var recorder = new UsageEventRecorder();

        var wasRecorded = recorder.TryRecord(new TrackEventRequestDto("totally_unknown_event", "tree"));

        wasRecorded.Should().BeFalse();
    }

    [Test]
    public void TryRecord_WithUnknownPage_ReturnsFalse()
    {
        var recorder = new UsageEventRecorder();

        var wasRecorded = recorder.TryRecord(new TrackEventRequestDto(TrackedEvents.PageViewEvent, "nonexistent_page"));

        wasRecorded.Should().BeFalse();
    }

    [Test]
    public void TryRecord_PageView_IncrementsPageViewCounterForThatPage()
    {
        var recorder = new UsageEventRecorder();
        var before = AppMetrics.PageViews.WithLabels("profile").Value;

        var wasRecorded = recorder.TryRecord(new TrackEventRequestDto(TrackedEvents.PageViewEvent, "profile"));

        wasRecorded.Should().BeTrue();
        AppMetrics.PageViews.WithLabels("profile").Value.Should().Be(before + 1);
    }

    [Test]
    public void TryRecord_UiEvent_IncrementsEventCounterForThatEventAndPage()
    {
        var recorder = new UsageEventRecorder();
        var before = AppMetrics.Events.WithLabels("start_dialog", "dialog").Value;

        var wasRecorded = recorder.TryRecord(new TrackEventRequestDto("start_dialog", "dialog"));

        wasRecorded.Should().BeTrue();
        AppMetrics.Events.WithLabels("start_dialog", "dialog").Value.Should().Be(before + 1);
    }
}
