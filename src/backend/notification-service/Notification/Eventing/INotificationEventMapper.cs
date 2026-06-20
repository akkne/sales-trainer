using Sellevate.BuildingBlocks.Eventing;
using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Eventing;

public interface INotificationEventMapper
{
    CreateNotificationRequest? Map(EventEnvelope envelope);
}
