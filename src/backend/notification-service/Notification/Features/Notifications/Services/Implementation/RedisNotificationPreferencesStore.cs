using System.Globalization;
using Sellevate.Notification.Common.Constants;
using Sellevate.Notification.Features.Notifications.Models;
using Sellevate.Notification.Features.Notifications.Services.Abstract;
using StackExchange.Redis;

namespace Sellevate.Notification.Features.Notifications.Services.Implementation;

/// <summary>
/// Q-4 (<c>docs/NIGHT_AUDIT_QUESTIONS.md</c>). Each person's two switches as one small Redis hash at
/// <see cref="RedisKeys.NotificationPreferences"/>.
///
/// <para>
/// A hash rather than a serialized JSON blob, and unlike the inbox it needs no Lua: every write here
/// replaces the whole (tiny) value from a single request handler, so there is no read-modify-write
/// for a concurrent writer to interleave with — the last save wins, which is what a settings screen
/// means. The inbox's scripts exist because two dispatchers and an API request mutate one list
/// concurrently; nothing here does.
/// </para>
///
/// <para>
/// <b>No TTL</b>, deliberately, and for the same reason as the user replica: this is a durable
/// statement a person made about their own inbox, not a cache of one. An expiring preference would
/// quietly turn "do not send me product updates" back on some weeks later, which is precisely the
/// failure the switch existed to prevent.
/// </para>
///
/// <para>
/// A malformed or partially-written hash reads as "never set" rather than raising: the settings
/// screen showing the documented defaults is a recoverable state the user can correct with one
/// click, whereas a 500 leaves them unable to set anything at all. The read never invents a
/// stored-ness it cannot prove — an unparseable timestamp means no <c>UpdatedAt</c>, which
/// <see cref="NotificationPreferences.IsDefault"/> then reports honestly.
/// </para>
/// </summary>
internal sealed class RedisNotificationPreferencesStore : INotificationPreferencesStore
{
    private const string PracticeRemindersField = "practiceReminders";
    private const string ProductUpdatesField = "productUpdates";
    private const string UpdatedAtField = "updatedAt";

    private readonly IConnectionMultiplexer _connection;

    public RedisNotificationPreferencesStore(IConnectionMultiplexer connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    private static RedisKey KeyFor(Guid userId) => RedisKeys.NotificationPreferences(userId);

    public async Task<NotificationPreferences?> GetAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var entries = await _connection.GetDatabase().HashGetAllAsync(KeyFor(userId));
        if (entries.Length == 0)
        {
            return null;
        }

        var fields = entries.ToDictionary(entry => (string)entry.Name!, entry => (string?)entry.Value);

        var updatedAt = ParseTimestamp(fields.GetValueOrDefault(UpdatedAtField));
        if (updatedAt is null)
        {
            // A hash with no readable write time is not a preference anyone can be shown to have
            // saved, so it is reported as absent rather than as a preference dated "now".
            return null;
        }

        return new NotificationPreferences(
            ParseFlag(fields.GetValueOrDefault(PracticeRemindersField),
                NotificationPreferences.Default.ArePracticeRemindersEnabled),
            ParseFlag(fields.GetValueOrDefault(ProductUpdatesField),
                NotificationPreferences.Default.AreProductUpdatesEnabled),
            updatedAt);
    }

    public async Task<NotificationPreferences> SaveAsync(
        Guid userId,
        bool arePracticeRemindersEnabled,
        bool areProductUpdatesEnabled,
        CancellationToken cancellationToken = default)
    {
        var updatedAt = DateTime.UtcNow;

        await _connection.GetDatabase().HashSetAsync(
            KeyFor(userId),
            [
                new HashEntry(PracticeRemindersField, arePracticeRemindersEnabled),
                new HashEntry(ProductUpdatesField, areProductUpdatesEnabled),
                new HashEntry(UpdatedAtField, updatedAt.ToString("O", CultureInfo.InvariantCulture)),
            ]);

        return new NotificationPreferences(
            arePracticeRemindersEnabled, areProductUpdatesEnabled, updatedAt);
    }

    public Task RemoveAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _connection.GetDatabase().KeyDeleteAsync(KeyFor(userId));

    /// <summary>
    /// Accepts what <c>HashEntry(name, bool)</c> writes ("1"/"0") as well as "true"/"false", so a row
    /// written by hand during an incident is still readable. Anything else falls back to the
    /// documented default for that switch rather than to <see langword="false"/> — guessing "off"
    /// for practice reminders would silently opt someone out of the thing they never turned off.
    /// </summary>
    private static bool ParseFlag(string? raw, bool fallback) => raw switch
    {
        "1" => true,
        "0" => false,
        _ => bool.TryParse(raw, out var parsed) ? parsed : fallback,
    };

    private static DateTime? ParseTimestamp(string? raw) =>
        DateTime.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : null;
}
