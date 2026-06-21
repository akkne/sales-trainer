using System.Text;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.Notification.Common.Constants;
using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Eventing;

internal sealed class NotificationEventMapper : INotificationEventMapper
{
    private const int ChatPreviewMaximumLength = 160;

    /// <summary>
    /// Truncates <paramref name="value"/> to at most <paramref name="maxRunes"/> Unicode
    /// scalar values (runes), appending "…" when truncation occurs. This avoids splitting
    /// a surrogate pair that would result from a naive <c>value[..n]</c> slice.
    /// </summary>
    private static string TruncateOnRuneBoundary(string value, int maxRunes)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var runeCount = 0;
        var charIndex = 0;
        while (charIndex < value.Length)
        {
            Rune.DecodeFromUtf16(value.AsSpan(charIndex), out _, out var charsConsumed);
            if (runeCount == maxRunes)
            {
                // We are past the limit — truncation needed
                return string.Concat(value.AsSpan(0, charIndex), "…");
            }
            runeCount++;
            charIndex += charsConsumed;
        }

        return value;
    }

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

        // NO4a: truncate on a rune (Unicode scalar value) boundary so we never split a
        // surrogate pair. String.Length counts UTF-16 code units, not grapheme clusters,
        // so a naive [..160] can produce an ill-formed string when a supplementary
        // character (emoji, rare CJK, etc.) straddles the cut point.
        var preview = TruncateOnRuneBoundary(payload.Preview ?? string.Empty, ChatPreviewMaximumLength);

        return new CreateNotificationRequest(
            payload.RecipientId,
            NotificationType.ChatMessageReceived,
            NotificationTitles.ChatMessageReceived,
            $"{payload.SenderName}: {preview}",
            actionUrl,
            payload.ConversationId?.ToString());
    }
}
