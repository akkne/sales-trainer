using Sellevate.Analytics.Features.Tracking.Constants;
using Sellevate.Analytics.Features.Tracking.Models;
using Sellevate.Analytics.Features.Tracking.Services.Abstract;
using Sellevate.Analytics.Infrastructure.Metrics;

namespace Sellevate.Analytics.Features.Tracking.Services.Implementation;

internal sealed class UsageEventRecorder : IUsageEventRecorder
{
    public bool TryRecord(TrackEventRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TrackedEvents.IsKnownEvent(request.Event) || !TrackedEvents.IsKnownPage(request.Page))
        {
            return false;
        }

        if (request.Event == TrackedEvents.PageViewEvent)
        {
            AppMetrics.PageViews.WithLabels(request.Page).Inc();
        }
        else
        {
            AppMetrics.Events.WithLabels(request.Event, request.Page).Inc();
        }

        return true;
    }
}
