namespace Sellevate.Notification.Features.Notifications.Models;

/// <summary>
/// Q-4 (<c>docs/NIGHT_AUDIT_QUESTIONS.md</c>). One person's answer to the two switches on
/// <c>/settings</c>. Until now these lived only in the browser's <c>localStorage</c>: they did not
/// survive a change of device and — worse — nothing on the server could read them, so any future
/// "product updates" mailer would have gone out to people who had explicitly switched it off.
///
/// <para>
/// <see cref="IsDefault"/> is what makes the one-time migration of those browser values possible,
/// and it is why this is a record rather than two bare booleans: a GET has to be able to say
/// "nobody has ever set these" as distinct from "somebody set them and happened to pick the
/// defaults". Without that distinction, the first read after the deploy would look identical for a
/// brand-new user and for someone who had carefully switched product updates off two months ago,
/// and the migration would have to either overwrite real answers or skip them all.
/// </para>
/// </summary>
public sealed record NotificationPreferences(
    bool ArePracticeRemindersEnabled,
    bool AreProductUpdatesEnabled,
    DateTime? UpdatedAt)
{
    /// <summary>
    /// Reminders default on and product updates default off — the same defaults the browser store
    /// used, so nobody's effective settings change on the deploy that introduces this. They are
    /// declared here, once, because the frontend now reads them from the server instead of
    /// re-deriving them: two copies of a default is how the two ends drift apart.
    /// </summary>
    public static NotificationPreferences Default { get; } = new(
        ArePracticeRemindersEnabled: true,
        AreProductUpdatesEnabled: false,
        UpdatedAt: null);

    /// <summary>
    /// True when this is <see cref="Default"/> because nothing was stored, not because someone
    /// chose values equal to it. Derived from <see cref="UpdatedAt"/> rather than stored separately:
    /// a write always stamps a time, so "never written" and "no timestamp" are the same fact and
    /// cannot disagree.
    /// </summary>
    public bool IsDefault => UpdatedAt is null;
}
