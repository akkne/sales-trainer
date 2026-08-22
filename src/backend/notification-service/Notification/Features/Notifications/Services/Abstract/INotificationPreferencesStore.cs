using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Features.Notifications.Services.Abstract;

/// <summary>
/// Q-4 (<c>docs/NIGHT_AUDIT_QUESTIONS.md</c>). Reads and writes one person's notification switches.
///
/// <para>
/// Unlike <see cref="INotificationStore"/>, no member takes an organization — deliberately, and it is
/// the whole reason this is a separate interface rather than four more methods on that one. A
/// notification preference belongs to the identity, not to a seat in one customer's organization
/// (see <c>RedisKeys.NotificationPreferences</c>), and a signature that cannot accept an
/// organization is a stronger statement of that than a comment saying it is ignored.
/// </para>
/// </summary>
public interface INotificationPreferencesStore
{
    /// <summary>
    /// Returns null when this person has never saved a preference — not the defaults. Only the
    /// caller can decide whether "never set" should read as a default or trigger the browser-value
    /// migration, and a store that quietly substituted defaults would take that choice away.
    /// </summary>
    Task<NotificationPreferences?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Replaces both switches and stamps the write time.</summary>
    Task<NotificationPreferences> SaveAsync(
        Guid userId,
        bool arePracticeRemindersEnabled,
        bool areProductUpdatesEnabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the row entirely, so the person reads as "never set" again. Exists for the
    /// <c>user.deleted</c> path: a preference outliving its identity is the kind of orphan that
    /// silently applies to whoever inherits the id, and the user directory already removes its own
    /// projection on the same event.
    /// </summary>
    Task RemoveAsync(Guid userId, CancellationToken cancellationToken = default);
}
