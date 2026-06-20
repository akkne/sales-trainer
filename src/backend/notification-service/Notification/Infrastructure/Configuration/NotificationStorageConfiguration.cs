namespace Sellevate.Notification.Infrastructure.Configuration;

public sealed class NotificationStorageConfiguration
{
    public const string SectionName = "NotificationStorage";

    public int InboxCapacity { get; init; } = 100;

    public int RetentionDays { get; init; } = 30;
}
