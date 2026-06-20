using Sellevate.BuildingBlocks.Eventing;
using Sellevate.Notification.Common.Constants;
using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Eventing;

internal sealed class NotificationEventMapper : INotificationEventMapper
{
    private const int ChatPreviewMaximumLength = 160;

    public CreateNotificationRequest? Map(EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return envelope.Type switch
        {
            Topics.AchievementUnlocked => MapAchievementUnlocked(envelope),
            Topics.StreakMilestone => MapStreakMilestone(envelope),
            Topics.FriendRequestReceived => MapFriendRequestReceived(envelope),
            Topics.FriendRequestAccepted => MapFriendRequestAccepted(envelope),
            Topics.ChatMessageSent => MapChatMessageSent(envelope),
            _ => null
        };
    }

    private static CreateNotificationRequest? MapAchievementUnlocked(EventEnvelope envelope)
    {
        var payload = envelope.DataAs<AchievementUnlockedEvent>();
        if (payload is null || string.IsNullOrWhiteSpace(payload.Title))
        {
            return null;
        }

        return new CreateNotificationRequest(
            payload.UserId,
            NotificationType.AchievementUnlocked,
            NotificationTitles.AchievementUnlocked,
            payload.Title,
            NotificationActionRoutes.Profile,
            payload.AchievementKey);
    }

    private static CreateNotificationRequest? MapStreakMilestone(EventEnvelope envelope)
    {
        var payload = envelope.DataAs<StreakMilestoneEvent>();
        if (payload is null || payload.DayCount <= 0)
        {
            return null;
        }

        var body = payload.BonusXp > 0
            ? $"You reached a {payload.DayCount}-day streak and earned {payload.BonusXp} bonus XP."
            : $"You reached a {payload.DayCount}-day streak.";

        return new CreateNotificationRequest(
            payload.UserId,
            NotificationType.StreakMilestone,
            NotificationTitles.StreakMilestone,
            body,
            NotificationActionRoutes.Profile,
            payload.DayCount.ToString());
    }

    private static CreateNotificationRequest? MapFriendRequestReceived(EventEnvelope envelope)
    {
        var payload = envelope.DataAs<FriendRequestReceivedEvent>();
        if (payload is null || string.IsNullOrWhiteSpace(payload.RequesterName))
        {
            return null;
        }

        return new CreateNotificationRequest(
            payload.RecipientId,
            NotificationType.FriendRequestReceived,
            NotificationTitles.FriendRequestReceived,
            $"{payload.RequesterName} sent you a friend request.",
            NotificationActionRoutes.FriendRequests,
            payload.FriendshipId?.ToString());
    }

    private static CreateNotificationRequest? MapFriendRequestAccepted(EventEnvelope envelope)
    {
        var payload = envelope.DataAs<FriendRequestAcceptedEvent>();
        if (payload is null || string.IsNullOrWhiteSpace(payload.AccepterName))
        {
            return null;
        }

        var actionUrl = payload.AccepterId is { } accepterId
            ? NotificationActionRoutes.FriendProfile(accepterId)
            : NotificationActionRoutes.FriendRequests;

        return new CreateNotificationRequest(
            payload.RecipientId,
            NotificationType.FriendRequestAccepted,
            NotificationTitles.FriendRequestAccepted,
            $"{payload.AccepterName} accepted your friend request.",
            actionUrl,
            payload.AccepterId?.ToString());
    }

    private static CreateNotificationRequest? MapChatMessageSent(EventEnvelope envelope)
    {
        var payload = envelope.DataAs<ChatMessageSentEvent>();
        if (payload is null || string.IsNullOrWhiteSpace(payload.SenderName))
        {
            return null;
        }

        var actionUrl = payload.ConversationId is { } conversationId
            ? NotificationActionRoutes.ChatConversation(conversationId)
            : null;

        var preview = payload.Preview ?? string.Empty;
        if (preview.Length > ChatPreviewMaximumLength)
        {
            preview = preview[..ChatPreviewMaximumLength];
        }

        return new CreateNotificationRequest(
            payload.RecipientId,
            NotificationType.ChatMessageReceived,
            NotificationTitles.ChatMessageReceived,
            $"{payload.SenderName}: {preview}",
            actionUrl,
            payload.ConversationId?.ToString());
    }
}
