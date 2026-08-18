using Sellevate.Analytics.Features.Tracking.Models;

namespace Sellevate.Analytics.Features.Tracking.Services.Abstract;

/// <summary>
/// Folds a client-reported usage event into the page-view and UI-event counters.
/// </summary>
public interface IUsageEventRecorder
{
    /// <summary>
    /// Returns <c>false</c> when either the event or the page is outside the server-side whitelist,
    /// having recorded nothing. The whitelist is not input validation for its own sake: both values
    /// become Prometheus label values, so accepting an arbitrary one would let a client grow the
    /// metrics store one time series per request.
    /// </summary>
    bool TryRecord(TrackEventRequestDto request);
}
