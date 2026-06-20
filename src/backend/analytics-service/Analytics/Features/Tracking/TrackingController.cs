using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Analytics.Common;
using Sellevate.Analytics.Common.Constants;
using Sellevate.Analytics.Features.Presence.Services.Abstract;
using Sellevate.Analytics.Features.Tracking.Models;
using Sellevate.Analytics.Features.Tracking.Services.Abstract;
using Sellevate.Analytics.Infrastructure.Metrics;

namespace Sellevate.Analytics.Features.Tracking;

[ApiController]
[Route(RouteConstants.TrackingBase)]
[Authorize]
public sealed class TrackingController : ControllerBase
{
    private readonly IUsageEventRecorder _usageEventRecorder;
    private readonly IPresenceTracker _presenceTracker;
    private readonly ILogger<TrackingController> _logger;

    public TrackingController(
        IUsageEventRecorder usageEventRecorder,
        IPresenceTracker presenceTracker,
        ILogger<TrackingController> logger)
    {
        ArgumentNullException.ThrowIfNull(usageEventRecorder);
        ArgumentNullException.ThrowIfNull(presenceTracker);
        ArgumentNullException.ThrowIfNull(logger);
        _usageEventRecorder = usageEventRecorder;
        _presenceTracker = presenceTracker;
        _logger = logger;
    }

    [HttpPost(RouteConstants.TrackingEvents)]
    public IActionResult TrackEvent([FromBody] TrackEventRequestDto request)
    {
        if (!_usageEventRecorder.TryRecord(request))
        {
            return BadRequest(new { message = ErrorMessages.UnknownEventOrPage });
        }

        return NoContent();
    }

    [HttpPost(RouteConstants.TrackingPresencePing)]
    public async Task<IActionResult> Ping(CancellationToken cancellationToken)
    {
        var userId = CurrentUserAccessor.ResolveUserId(HttpContext);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { message = ErrorMessages.MissingUserIdentity });
        }

        AppMetrics.AuthenticatedRequests.Inc();

        try
        {
            await _presenceTracker.MarkSeenAsync(userId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Failed to mark user presence");
        }

        return NoContent();
    }
}
