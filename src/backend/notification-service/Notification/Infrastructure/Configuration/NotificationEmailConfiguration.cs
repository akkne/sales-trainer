namespace Sellevate.Notification.Infrastructure.Configuration;

/// <summary>
/// Tunables for email notifications. <see cref="ChatUnreadDelayMinutes"/> is the grace period
/// before an unread direct message is emailed; the dispatcher wakes every
/// <see cref="DispatcherPollIntervalSeconds"/> to flush messages whose grace period has elapsed.
/// </summary>
public sealed class NotificationEmailConfiguration
{
    public const string SectionName = "NotificationEmail";

    /// <summary>How long a chat message may sit unread before it is emailed.</summary>
    public int ChatUnreadDelayMinutes { get; init; } = 5;

    /// <summary>How often the delayed-email dispatcher polls for due messages.</summary>
    public int DispatcherPollIntervalSeconds { get; init; } = 30;

    /// <summary>Safety TTL on pending/read bookkeeping keys in Redis so they self-clean.</summary>
    public int BookkeepingRetentionHours { get; init; } = 24;
}
