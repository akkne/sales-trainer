using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellevate.Notification.Common.Constants;
using Sellevate.Notification.Features.Notifications.Models;
using Sellevate.Notification.Features.Notifications.Services.Abstract;

namespace Sellevate.Notification.Features.Notifications.Endpoints;

/// <summary>
/// Q-4 (<c>docs/NIGHT_AUDIT_QUESTIONS.md</c>). The two switches on <c>/settings</c> — practice
/// reminders and product updates — now have somewhere to live that the server can read.
///
/// <para>
/// Before this they existed only in the browser's <c>localStorage</c>. That had two consequences,
/// and the second is the one that mattered: they did not survive a change of device or a cleared
/// cache, and <em>nothing on the backend could read them</em>, so the first real "product updates"
/// mailer would have gone out to everyone including the people who had explicitly switched it off.
/// The audit confirmed this was not "the frontend never called a finished API" — no such API, model
/// or storage existed in any service.
/// </para>
///
/// <para>
/// <b>Deliberately not <c>[TenantScoped]</c></b>, unlike <see cref="NotificationController"/> next
/// to it. Every route there reads or writes one organization's inbox; these two read and write a
/// statement a person made about their own inbox. An identity in this product is cross-organization
/// (docs/TENANCY/TENANCY.md §4.2), so scoping the preference to an organization would mean a
/// salesperson who belongs to two of them has to switch reminders off twice — and would keep getting
/// them from whichever organization they were not looking at. The stored row carries nothing
/// org-scoped: two booleans and a timestamp. See <c>RedisKeys.NotificationPreferences</c>.
/// </para>
///
/// <para>
/// Both routes address the caller's own row, taken from the token. There is no route, query or body
/// field naming a user, so there is no cross-user read or write to gate in the first place.
/// </para>
/// </summary>
[ApiController]
[Route(RouteConstants.Preferences)]
[Authorize]
public sealed class NotificationPreferencesController : ControllerBase
{
    private readonly INotificationPreferencesStore _preferencesStore;

    public NotificationPreferencesController(INotificationPreferencesStore preferencesStore)
    {
        ArgumentNullException.ThrowIfNull(preferencesStore);
        _preferencesStore = preferencesStore;
    }

    /// <summary>
    /// Always answers with a usable pair of switches: the stored row when there is one, otherwise the
    /// documented defaults flagged <c>isDefault: true</c>. The flag is not decoration — it is what
    /// lets the client tell "nobody has ever set these" from "someone set them and chose the
    /// defaults", which is the only way its one-time migration of the old browser values can avoid
    /// either overwriting real answers or skipping all of them.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<NotificationPreferencesDto>> GetPreferences(
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var stored = await _preferencesStore.GetAsync(userId, cancellationToken);
        return Ok(NotificationPreferencesDto.From(stored ?? NotificationPreferences.Default));
    }

    /// <summary>
    /// Replaces both switches. Returns the saved state so the client renders what the server actually
    /// holds rather than what it hoped it sent — and so <c>isDefault</c> flips to false in the same
    /// round trip, retiring the migration path for this user immediately.
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<NotificationPreferencesDto>> UpdatePreferences(
        [FromBody] UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { message = "Both switches must be supplied." });
        }

        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var saved = await _preferencesStore.SaveAsync(
            userId, request.PracticeReminders, request.ProductUpdates, cancellationToken);

        return Ok(NotificationPreferencesDto.From(saved));
    }

    /// <summary>
    /// The same claim pair <see cref="NotificationController"/> reads, for the same reason: the
    /// subject is taken from the token and never from the request, so no caller can name someone
    /// else's preferences.
    /// </summary>
    private bool TryGetCurrentUserId(out Guid userId)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(ClaimTypeNames.Subject);

        return Guid.TryParse(rawUserId, out userId);
    }
}
