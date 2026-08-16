using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Notification.Common.Constants;
using Sellevate.Notification.Features.Notifications.Models;
using Sellevate.Notification.Features.Notifications.Services.Abstract;

namespace Sellevate.Notification.Features.Notifications.Endpoints;

/// <summary>
/// Phase 40.13 marked the whole controller <see cref="TenantScopedAttribute"/>. Every route here
/// reads or writes one organization's inbox, so a request without the gateway-validated
/// organization header is refused by the tenant middleware rather than reaching a service that
/// would have to raise anyway — the difference is a 403 instead of a 500.
/// </summary>
[ApiController]
[Route(RouteConstants.NotificationsBase)]
[Authorize]
[TenantScoped]
public sealed class NotificationController : ControllerBase
{
    private const int DefaultPageLimit = 20;
    private const int MaximumPageLimit = 100;

    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService)
    {
        ArgumentNullException.ThrowIfNull(notificationService);
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetRecentNotifications(
        [FromQuery] int limit = DefaultPageLimit,
        [FromQuery] bool includeRead = true,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var recipientUserId))
        {
            return Unauthorized();
        }

        var clampedLimit = Math.Clamp(limit, 1, MaximumPageLimit);
        var notifications = await _notificationService.GetRecentAsync(
            recipientUserId, clampedLimit, includeRead, cancellationToken);
        return Ok(notifications);
    }

    [HttpGet(RouteConstants.UnreadCount)]
    public async Task<ActionResult<UnreadNotificationCountDto>> GetUnreadCount(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var recipientUserId))
        {
            return Unauthorized();
        }

        var unreadCount = await _notificationService.GetUnreadCountAsync(recipientUserId, cancellationToken);
        return Ok(new UnreadNotificationCountDto(unreadCount));
    }

    [HttpPut(RouteConstants.MarkSingleAsRead)]
    public async Task<IActionResult> MarkAsRead(Guid notificationId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var recipientUserId))
        {
            return Unauthorized();
        }

        try
        {
            await _notificationService.MarkAsReadAsync(recipientUserId, notificationId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpPut(RouteConstants.MarkAllAsRead)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var recipientUserId))
        {
            return Unauthorized();
        }

        await _notificationService.MarkAllAsReadAsync(recipientUserId, cancellationToken);
        return NoContent();
    }

    private bool TryGetCurrentUserId(out Guid recipientUserId)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(rawUserId, out recipientUserId);
    }
}
