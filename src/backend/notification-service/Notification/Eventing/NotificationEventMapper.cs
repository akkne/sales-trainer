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

        // Reserve one rune for the ellipsis so the returned string (including "…") never
        // exceeds maxRunes — keeps the total within the documented preview budget.
        var runeCount = 0;
        var charIndex = 0;
        var ellipsisCutIndex = -1;
        while (charIndex < value.Length)
        {
            Rune.DecodeFromUtf16(value.AsSpan(charIndex), out _, out var charsConsumed);
            if (runeCount == maxRunes - 1)
            {
                ellipsisCutIndex = charIndex;
            }

            runeCount++;
            charIndex += charsConsumed;
        }

        if (runeCount <= maxRunes)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, ellipsisCutIndex), "…");
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
            Topics.DiscussReplyCreated => MapDiscussReplyCreated(envelope),
            Topics.LeagueUpdated => MapLeagueUpdated(envelope),
            Topics.CompanyFollowUpDue => MapCompanyFollowUpDue(envelope),
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
            payload.FriendshipId?.ToString(),
            SendEmail: true);
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
            payload.AccepterId?.ToString(),
            SendEmail: true);
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
        // SendEmail stays false: the unread-chat email is dispatched on a delayed path,
        // not at notification-creation time (see DelayedChatEmailScheduler).
    }

    private static CreateNotificationRequest? MapDiscussReplyCreated(EventEnvelope envelope)
    {
        var payload = envelope.DataAs<DiscussReplyCreatedEvent>();
        if (payload is null
            || payload.RecipientId == Guid.Empty
            || string.IsNullOrWhiteSpace(payload.ReplyAuthorName))
        {
            return null;
        }

        // Never notify someone about their own reply to their own thread.
        if (payload.RecipientId == payload.ReplyAuthorId)
        {
            return null;
        }

        var threadTitle = string.IsNullOrWhiteSpace(payload.ThreadTitle)
            ? "your discussion"
            : $"\"{payload.ThreadTitle.Trim()}\"";
        var preview = TruncateOnRuneBoundary(payload.Preview ?? string.Empty, ChatPreviewMaximumLength);
        var body = string.IsNullOrWhiteSpace(preview)
            ? $"{payload.ReplyAuthorName} replied to {threadTitle}."
            : $"{payload.ReplyAuthorName} replied to {threadTitle}: {preview}";

        return new CreateNotificationRequest(
            payload.RecipientId,
            NotificationType.DiscussReplyReceived,
            NotificationTitles.DiscussReplyReceived,
            body,
            NotificationActionRoutes.DiscussThread(payload.ThreadId),
            // Dedupe on the reply id — a Kafka replay of the same reply is collapsed, while
            // distinct replies (even from the same author) remain separate notifications.
            payload.ReplyId.ToString(),
            SendEmail: true);
    }

    private static CreateNotificationRequest? MapLeagueUpdated(EventEnvelope envelope)
    {
        var payload = envelope.DataAs<LeagueUpdatedEvent>();
        if (payload is null || payload.UserId == Guid.Empty)
        {
            return null;
        }

        var newTier = FormatTier(payload.NewTier);
        var body = payload.Outcome switch
        {
            "promoted" => $"You were promoted to the {newTier} league. Keep it up!",
            "demoted" => $"You dropped to the {newTier} league. Time for a comeback!",
            _ => $"A new league week has started. You're in the {newTier} league."
        };

        return new CreateNotificationRequest(
            payload.UserId,
            NotificationType.LeagueUpdated,
            NotificationTitles.LeagueUpdated,
            body,
            NotificationActionRoutes.League,
            // Dedupe on the resulting league id — one notification per user per weekly rollover,
            // while replays of the same rollover event are collapsed.
            payload.LeagueId.ToString(),
            SendEmail: true);
    }

    private static CreateNotificationRequest? MapCompanyFollowUpDue(EventEnvelope envelope)
    {
        var payload = envelope.DataAs<CompanyFollowUpDueEvent>();
        if (payload is null || payload.UserId == Guid.Empty || string.IsNullOrWhiteSpace(payload.CompanyName))
        {
            return null;
        }

        var title = $"Пора связаться с {payload.CompanyName}";
        var body = string.IsNullOrWhiteSpace(payload.Note)
            ? "Настало время запланированного контакта."
            : payload.Note.Trim();

        return new CreateNotificationRequest(
            payload.UserId,
            NotificationType.CompanyFollowUpDue,
            title,
            body,
            NotificationActionRoutes.CompanyDetails(payload.CompanyId),
            // Dedupe on company + the specific due date, not just the company: company-service
            // resets FollowUpNotifiedAt on reschedule, so a later due date for the same company
            // must produce a fresh notification rather than being suppressed by the still-inboxed
            // reminder for the earlier date. Uses the "O" (round-trip) format so the key is
            // exact to the tick and reproducible byte-for-byte on both the producer's original
            // DateTime and any re-serialization here — a lossier format (e.g. seconds-only) could
            // collapse two distinct-but-close due dates onto the same dedupe key.
            $"{payload.CompanyId}:{payload.NextActionAt:O}");
        // SendEmail stays false: follow-up due reminders are in-app only per product spec.
    }

    private static string FormatTier(string? tierKey)
    {
        if (string.IsNullOrWhiteSpace(tierKey))
        {
            return "new";
        }

        return char.ToUpperInvariant(tierKey[0]) + tierKey[1..].ToLowerInvariant();
    }
}
