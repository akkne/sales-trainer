using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesTrainer.Api.Features.Notifications.Models;
using SalesTrainer.Api.Features.Notifications.Services.Abstract;

namespace SalesTrainer.Api.Features.Notifications;

[ApiController]
[Route("notifications")]
[Authorize]
public class NotificationController(INotificationService notificationService) : ControllerBase
{
    private const int DefaultPageLimit = 20;
    private const int MaximumPageLimit = 100;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetRecentNotifications(
        [FromQuery] int limit = DefaultPageLimit,
        [FromQuery] bool includeRead = true,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var clampedLimit = Math.Clamp(limit, 1, MaximumPageLimit);
        var notifications = await notificationService.GetRecentAsync(userId, clampedLimit, includeRead, cancellationToken);
        return Ok(notifications);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadNotificationCountDto>> GetUnreadCount(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var unreadCount = await notificationService.GetUnreadCountAsync(userId, cancellationToken);
        return Ok(new UnreadNotificationCountDto(unreadCount));
    }

    [HttpPut("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid notificationId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        try
        {
            await notificationService.MarkAsReadAsync(userId, notificationId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        await notificationService.MarkAllAsReadAsync(userId, cancellationToken);
        return NoContent();
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(rawUserId, out userId);
    }
}
