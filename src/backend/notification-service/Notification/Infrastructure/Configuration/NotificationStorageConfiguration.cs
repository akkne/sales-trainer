namespace Sellevate.Notification.Infrastructure.Configuration;

/// <summary>
/// Bounds on the Redis-only inbox. The service has no database and runs no cleanup job, so these two
/// values are the whole of its retention policy: <see cref="InboxCapacity"/> caps per-user memory by
/// trimming the oldest notification on every write, and <see cref="RetentionDays"/> is the key TTL
/// that expires an idle inbox passively. Lowering either silently discards existing notifications.
/// </summary>
public sealed class NotificationStorageConfiguration
{
    public const string SectionName = "NotificationStorage";

    /// <summary>Most notifications kept per user; older ones are trimmed away on write.</summary>
    public int InboxCapacity { get; init; } = 100;

    /// <summary>TTL refreshed on every write to the inbox key. Clamped to a minimum of one day.</summary>
    public int RetentionDays { get; init; } = 30;
}
