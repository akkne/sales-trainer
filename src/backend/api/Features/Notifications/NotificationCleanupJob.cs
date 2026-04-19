using Microsoft.Extensions.Logging;
using SalesTrainer.Api.Features.Notifications.Services.Abstract;

namespace SalesTrainer.Api.Features.Notifications;

public sealed class NotificationCleanupJob(
    INotificationService notificationService,
    ILogger<NotificationCleanupJob> logger)
{
    private const int RetentionDays = 30;

    public async Task ExecuteAsync()
    {
        var thresholdUtc = DateTime.UtcNow.AddDays(-RetentionDays);
        var deletedCount = await notificationService.DeleteReadNotificationsOlderThanAsync(thresholdUtc);

        logger.LogInformation(
            "NotificationCleanupJob removed {DeletedCount} read notifications older than {ThresholdUtc}.",
            deletedCount,
            thresholdUtc);
    }
}
