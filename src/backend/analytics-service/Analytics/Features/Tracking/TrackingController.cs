using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Analytics.Common;
using Sellevate.Analytics.Common.Constants;
using Sellevate.Analytics.Features.Presence.Services.Abstract;
using Sellevate.Analytics.Features.Tracking.Models;
using Sellevate.Analytics.Features.Tracking.Services.Abstract;
using Sellevate.Analytics.Infrastructure.Metrics;
using Sellevate.BuildingBlocks.Tenancy;

namespace Sellevate.Analytics.Features.Tracking;

[ApiController]
[Route(RouteConstants.TrackingBase)]
[Authorize]
public sealed class TrackingController : ControllerBase
{
    private readonly IUsageEventRecorder _usageEventRecorder;
    private readonly IPresenceTracker _presenceTracker;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<TrackingController> _logger;

    public TrackingController(
        IUsageEventRecorder usageEventRecorder,
        IPresenceTracker presenceTracker,
        ITenantContext tenantContext,
        ILogger<TrackingController> logger)
    {
        ArgumentNullException.ThrowIfNull(usageEventRecorder);
        ArgumentNullException.ThrowIfNull(presenceTracker);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(logger);
        _usageEventRecorder = usageEventRecorder;
        _presenceTracker = presenceTracker;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    [HttpPost(RouteConstants.TrackingEvents)]
    public IActionResult TrackEvent([FromBody] TrackEventRequestDto? request)
    {
        if (request is null)
        {
            return BadRequest(new { message = ErrorMessages.MissingOrMalformedBody });
        }

        if (!_usageEventRecorder.TryRecord(request))
        {
            return BadRequest(new { message = ErrorMessages.UnknownEventOrPage });
        }

        return NoContent();
    }

    /// <summary>
    /// Phase 40.13 marked this route <see cref="TenantScopedAttribute"/>. Presence is now stored
    /// per organization, so a ping that arrives without the gateway-validated organization header
    /// has nowhere correct to go: the middleware answers 403 before the action runs, rather than
    /// letting the ping land in a shared bucket. This is a heartbeat the client fires on a timer,
    /// so a rejected ping costs a missing dot on an operational gauge and nothing a user sees.
    /// </summary>
    [HttpPost(RouteConstants.TrackingPresencePing)]
    [TenantScoped]
    public async Task<IActionResult> Ping(CancellationToken cancellationToken)
    {
        var userId = CurrentUserAccessor.ResolveUserId(HttpContext);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { message = ErrorMessages.MissingUserIdentity });
        }

        if (_tenantContext.OrganizationId is not { } organizationId)
        {
            return Forbid();
        }

        AppMetrics.AuthenticatedRequests.Inc();

        try
        {
            await _presenceTracker.MarkSeenAsync(organizationId, userId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Failed to mark user presence");
        }

        return NoContent();
    }
}
