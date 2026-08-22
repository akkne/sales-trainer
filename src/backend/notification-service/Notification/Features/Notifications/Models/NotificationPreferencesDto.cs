namespace Sellevate.Notification.Features.Notifications.Models;

/// <summary>
/// What <c>GET/PUT /notifications/preferences</c> answers with (Q-4).
///
/// <para>
/// <see cref="IsDefault"/> is on the wire on purpose: it is the one bit the frontend needs to run
/// its single-shot migration of the old browser-only values, and it stops being useful the moment
/// the user has saved once. See <see cref="NotificationPreferences.IsDefault"/>.
/// </para>
/// </summary>
public sealed record NotificationPreferencesDto(
    bool PracticeReminders,
    bool ProductUpdates,
    bool IsDefault)
{
    public static NotificationPreferencesDto From(NotificationPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        return new NotificationPreferencesDto(
            preferences.ArePracticeRemindersEnabled,
            preferences.AreProductUpdatesEnabled,
            preferences.IsDefault);
    }
}

/// <summary>
/// A full replacement of both switches, not a patch: the settings screen holds both values in view
/// and sends both, and an absent-means-unchanged body would make "switch this off" indistinguishable
/// from "leave it alone" on a field the caller genuinely wants set to false.
/// </summary>
public sealed record UpdateNotificationPreferencesRequest(
    bool PracticeReminders,
    bool ProductUpdates
);
