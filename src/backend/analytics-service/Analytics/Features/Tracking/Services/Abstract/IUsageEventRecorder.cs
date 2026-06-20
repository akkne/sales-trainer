using Sellevate.Analytics.Features.Tracking.Models;

namespace Sellevate.Analytics.Features.Tracking.Services.Abstract;

public interface IUsageEventRecorder
{
    bool TryRecord(TrackEventRequestDto request);
}
