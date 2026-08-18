using Sellevate.Analytics.Features.Tracking.Constants;
using Sellevate.Analytics.Features.Tracking.Models;
using Sellevate.Analytics.Features.Tracking.Services.Abstract;
using Sellevate.Analytics.Infrastructure.Metrics;

namespace Sellevate.Analytics.Features.Tracking.Services.Implementation;

/// <summary>
/// Increments one of the two usage counters after checking the request against the whitelist in
/// <see cref="TrackedEvents"/>. A page view goes to <c>app_page_views_total</c> and everything else
/// to <c>app_events_total</c>: the two are kept apart so a navigation cannot be confused with a
/// deliberate action in a dashboard.
/// </summary>
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
