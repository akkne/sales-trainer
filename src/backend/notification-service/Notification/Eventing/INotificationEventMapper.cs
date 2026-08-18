using Sellevate.BuildingBlocks.Eventing;
using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Eventing;

/// <summary>
/// Maps an integration event onto the notification it should become. Returning <c>null</c> means
/// "this event must not produce a notification" — a normal, deliberate outcome, not a failure — so a
/// caller must not retry or dead-letter on it.
/// </summary>
public interface INotificationEventMapper
{
    CreateNotificationRequest? Map(EventEnvelope envelope);
}
