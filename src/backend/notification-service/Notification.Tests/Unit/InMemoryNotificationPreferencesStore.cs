using Sellevate.Notification.Features.Notifications.Models;
using Sellevate.Notification.Features.Notifications.Services.Abstract;

namespace Sellevate.Notification.Tests.Unit;

/// <summary>
/// Q-4. Keyed by user alone and by nothing else — that is the contract under test, not an omission:
/// a fake that also keyed by organization would let a preference-per-organization regression pass,
/// which is exactly the behaviour the real key shape rules out.
/// </summary>
internal sealed class InMemoryNotificationPreferencesStore : INotificationPreferencesStore
{
    private readonly Dictionary<Guid, NotificationPreferences> _preferences = new();

    public int SaveCount { get; private set; }

    public Task<NotificationPreferences?> GetAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_preferences.GetValueOrDefault(userId));

    public Task<NotificationPreferences> SaveAsync(
        Guid userId,
        bool arePracticeRemindersEnabled,
        bool areProductUpdatesEnabled,
        CancellationToken cancellationToken = default)
    {
        SaveCount++;

        // The real store stamps the write time; without it `IsDefault` would keep reporting true
        // after a save and the fake would silently disagree with production on the one bit the
        // migration path reads.
        var saved = new NotificationPreferences(
            arePracticeRemindersEnabled,
            areProductUpdatesEnabled,
            new DateTime(2026, 8, 22, 12, 0, 0, DateTimeKind.Utc));

        _preferences[userId] = saved;
        return Task.FromResult(saved);
    }

    public Task RemoveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _preferences.Remove(userId);
        return Task.CompletedTask;
    }
}
