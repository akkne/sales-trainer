using System.Text;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.Notification.Common.Constants;
using Sellevate.Notification.Features.Notifications.Models;

namespace Sellevate.Notification.Eventing;

/// <summary>
/// Turns an integration event into the notification it deserves, or into nothing at all.
///
/// <para>
/// <b>Returning <c>null</c> is a first-class outcome, not an error path.</b> Every guard below is a
/// deliberate decision that a particular event must not become a notification — a blank name, a
/// self-reply, a digest in which nobody is actually late. Several of those guards are duplicated on
/// purpose in the producing service: a defence that lives in one service only is a defence one
/// refactor away from gone. Do not "simplify" a condition here on the grounds that the producer
/// already checks it; that is precisely why it is here twice.
/// </para>
///
/// <para>
/// The dedupe key each mapping puts in <c>RelatedEntityId</c> is the other load-bearing decision.
/// <c>NotificationService</c> collapses a second notification with the same
/// (recipient, type, key), so the key has to be exactly as coarse as the domain fact is repeatable:
/// too fine and a Kafka redelivery becomes a duplicate in somebody's inbox, too coarse and a
/// genuinely new event is swallowed by the stale notice it was supposed to replace. Each mapping
/// documents the choice it made.
/// </para>
/// </summary>
internal sealed class NotificationEventMapper : INotificationEventMapper
{
    /// <summary>
    /// The one-readable-line budget for a preview quoted inside a notification body, shared by the
    /// chat preview and by the quoted fragment of a coaching note; docs/NOTIFICATIONS.md records it
    /// as "160 characters". The two paths measure it in different units — runes for chat (see
    /// <see cref="TruncateOnRuneBoundary"/>) and UTF-16 code units for a quote (see
    /// <see cref="Shorten"/>) — because they were written apart, but the product budget is one.
    /// </summary>
    private const int PreviewMaximumLength = 160;

    /// <summary>Room reserved for <see cref="Ellipsis"/> when a quoted fragment is cut.</summary>
    private const int EllipsisReserveLength = 3;

    private const string Ellipsis = "…";

    /// <summary>
    /// Rendered <c>dd.MM.yyyy</c> rather than through a Russian long-date format on purpose: the
    /// container's culture data is not something this service controls, and a date the recipient
    /// cannot parse is worse than a plain one.
    /// </summary>
    private const string DeadlineFormat = "dd.MM.yyyy";

    /// <summary>
    /// The one outcome value that reads as "the manager was right". Arrives on the wire from
    /// learning-service, which owns the vocabulary; compared case-insensitively so the notice never
    /// silently degrades to the "score stands" wording on a casing change upstream.
    /// </summary>
    private const string UpheldOutcome = "upheld";

    /// <summary>
    /// Truncates <paramref name="value"/> to at most <paramref name="maximumRunes"/> Unicode scalar
    /// values (runes), appending <see cref="Ellipsis"/> when truncation occurs, and reserving one
    /// rune for it so the returned string never exceeds the budget.
    ///
    /// <para>
    /// Rune-aware rather than <c>value[..n]</c>: <c>String.Length</c> counts UTF-16 code units, not
    /// grapheme clusters, so a naive slice can land inside a surrogate pair and produce an
    /// ill-formed string whenever a supplementary character (emoji, rare CJK) straddles the cut.
    /// </para>
    /// </summary>
    private static string TruncateOnRuneBoundary(string value, int maximumRunes)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var runeCount = 0;
        var charIndex = 0;
        var ellipsisCutIndex = -1;
        while (charIndex < value.Length)
        {
            Rune.DecodeFromUtf16(value.AsSpan(charIndex), out _, out var charsConsumed);
            if (runeCount == maximumRunes - 1)
            {
                ellipsisCutIndex = charIndex;
            }

            runeCount++;
            charIndex += charsConsumed;
        }

        if (runeCount <= maximumRunes)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, ellipsisCutIndex), Ellipsis);
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
            Topics.CompanyFollowUpDue => MapCompanyFollowUpDue(envelope),
            Topics.AssignmentIssued => MapAssignmentIssued(envelope),
            Topics.AssignmentDeadlineApproaching => MapAssignmentDeadlineApproaching(envelope),
            Topics.AssignmentReminder => MapAssignmentReminder(envelope),
            Topics.AssignmentDeadlineDigest => MapAssignmentDeadlineDigest(envelope),
            Topics.DialogReviewDisputed => MapDialogReviewDisputed(envelope),
            Topics.DialogReviewCommented => MapDialogReviewCommented(envelope),
            Topics.DialogReviewResolved => MapDialogReviewResolved(envelope),
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

    /// <summary>
    /// <c>SendEmail</c> stays false here, unlike every other emailed type: the unread-chat email is
    /// dispatched on the delayed path (<c>RedisDelayedChatEmailScheduler</c>) once the grace period
    /// proves the message was never read, not at notification-creation time.
    /// </summary>
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

        var preview = TruncateOnRuneBoundary(payload.Preview ?? string.Empty, PreviewMaximumLength);

        return new CreateNotificationRequest(
            payload.RecipientId,
            NotificationType.ChatMessageReceived,
            NotificationTitles.ChatMessageReceived,
            $"{payload.SenderName}: {preview}",
            actionUrl,
            payload.ConversationId?.ToString());
    }

    /// <summary>
    /// Never notifies someone about their own reply to their own thread.
    ///
    /// <para>
    /// Dedupes on the reply id: a Kafka replay of the same reply is collapsed, while distinct
    /// replies — even consecutive ones from the same author — remain separate notifications.
    /// </para>
    /// </summary>
    private static CreateNotificationRequest? MapDiscussReplyCreated(EventEnvelope envelope)
    {
        var payload = envelope.DataAs<DiscussReplyCreatedEvent>();
        if (payload is null
            || payload.RecipientId == Guid.Empty
            || string.IsNullOrWhiteSpace(payload.ReplyAuthorName))
        {
            return null;
        }

        if (payload.RecipientId == payload.ReplyAuthorId)
        {
            return null;
        }

        var threadTitle = string.IsNullOrWhiteSpace(payload.ThreadTitle)
            ? "your discussion"
            : $"\"{payload.ThreadTitle.Trim()}\"";
        var preview = TruncateOnRuneBoundary(payload.Preview ?? string.Empty, PreviewMaximumLength);
        var body = string.IsNullOrWhiteSpace(preview)
            ? $"{payload.ReplyAuthorName} replied to {threadTitle}."
            : $"{payload.ReplyAuthorName} replied to {threadTitle}: {preview}";

        return new CreateNotificationRequest(
            payload.RecipientId,
            NotificationType.DiscussReplyReceived,
            NotificationTitles.DiscussReplyReceived,
            body,
            NotificationActionRoutes.DiscussThread(payload.ThreadId),
            payload.ReplyId.ToString(),
            SendEmail: true);
    }

    /// <summary>
    /// Phase 40.23. The three assignment notices all address one person about one assignment, so
    /// they share a body shape and differ only in what they are trying to make happen.
    ///
    /// <para>
    /// Dedupes on the assignment alone. A person is issued an assignment once — 40.23's fan-out only
    /// ever adds recipients, never re-adds one — so a second event with this key is a Kafka
    /// redelivery and collapsing it is correct.
    /// </para>
    /// </summary>
    private static CreateNotificationRequest? MapAssignmentIssued(EventEnvelope envelope)
    {
        var payload = envelope.DataAs<AssignmentIssuedEvent>();
        if (payload is null || payload.UserId == Guid.Empty || string.IsNullOrWhiteSpace(payload.Title))
        {
            return null;
        }

        var title = payload.Title.Trim();
        var goal = payload.Goal?.Trim();
        var body = payload.Deadline is { } deadline
            ? $"«{title}» — до {deadline.ToString(DeadlineFormat)}."
            : $"«{title}» — без срока.";

        if (!string.IsNullOrEmpty(goal))
        {
            body += $" {goal}";
        }

        return new CreateNotificationRequest(
            payload.UserId,
            NotificationType.AssignmentIssued,
            NotificationTitles.AssignmentIssued,
            body,
            NotificationActionRoutes.Assignment(payload.AssignmentId),
            payload.AssignmentId.ToString(),
            SendEmail: true);
    }

    /// <summary>
    /// Phase 40.23. Dedupes on the assignment plus the exact due instant, for the same reason the
    /// digest below does: extending a deadline has to arm a fresh notice rather than be swallowed by
    /// the notice for the date that no longer applies.
    /// </summary>
    private static CreateNotificationRequest? MapAssignmentDeadlineApproaching(EventEnvelope envelope)
    {
        var payload = envelope.DataAs<AssignmentDeadlineApproachingEvent>();
        if (payload is null || payload.UserId == Guid.Empty || string.IsNullOrWhiteSpace(payload.Title))
        {
            return null;
        }

        var title = payload.Title.Trim();

        return new CreateNotificationRequest(
            payload.UserId,
            NotificationType.AssignmentDeadlineApproaching,
            NotificationTitles.AssignmentDeadlineApproaching,
            $"«{title}» нужно завершить до {payload.Deadline.ToString(DeadlineFormat)}.",
            NotificationActionRoutes.Assignment(payload.AssignmentId),
            $"{payload.AssignmentId}:{payload.Deadline:O}",
            SendEmail: true);
    }

    /// <summary>
    /// Phase 40.23. Dedupes on the assignment plus the <em>hour</em> the РОП pressed the button, so a
    /// second press tomorrow is a second reminder while a redelivery of today's is not. A reminder
    /// that could only ever be sent once would defeat the point of the button.
    ///
    /// <para>
    /// Phase 40.26 coarsened this from the exact instant to the hour, because that block put the
    /// button inside a notification sent to <em>every</em> administrator of the organization: five
    /// РОПs opening the same digest used to mean five separate reminders landing on the same manager
    /// within a minute. The whole feature depends on that inbox still being read.
    /// </para>
    /// </summary>
    private static CreateNotificationRequest? MapAssignmentReminder(EventEnvelope envelope)
    {
        var payload = envelope.DataAs<AssignmentReminderEvent>();
        if (payload is null || payload.UserId == Guid.Empty || string.IsNullOrWhiteSpace(payload.Title))
        {
            return null;
        }

        var title = payload.Title.Trim();
        var body = payload.Deadline is { } deadline
            ? $"Задание «{title}» ещё не завершено. Срок — {deadline.ToString(DeadlineFormat)}."
            : $"Задание «{title}» ещё не завершено.";

        return new CreateNotificationRequest(
            payload.UserId,
            NotificationType.AssignmentReminder,
            NotificationTitles.AssignmentReminder,
            body,
            NotificationActionRoutes.Assignment(payload.AssignmentId),
            $"{payload.AssignmentId}:{payload.RequestedAt:yyyy-MM-ddTHH}",
            SendEmail: true);
    }

    /// <summary>
    /// Phase 40.26. The one notice in this service addressed to a РОП about somebody else's work
    /// (docs/TENANCY/ASSIGNMENTS.md §5).
    ///
    /// <para>
    /// The body opens with names and the action url carries the reminder, because the roadmap's
    /// requirement is «не отчёт, который РОП может открыть, а адресный пуш с действием» — and a
    /// digest that reads "3 сотрудника не начали" sends its reader to look somewhere else, which is
    /// the report it was supposed to replace.
    /// </para>
    ///
    /// <para>
    /// <b>The <c>NotStartedCount &lt;= 0</c> guard is load-bearing.</b> Nobody having failed to start
    /// is the one message this notice must never be, and the producer already declines to publish
    /// that case — the guard is duplicated here because a defence that lives in one service only is
    /// a defence one refactor away from gone.
    /// </para>
    ///
    /// <para>
    /// Dedupes on the assignment plus the exact due instant, exactly as the manager's notice above:
    /// moving a deadline clears the producer's sent-ness stamp, and the fresh digest for the new date
    /// must not be swallowed by the one for the date that no longer applies.
    /// </para>
    /// </summary>
    private static CreateNotificationRequest? MapAssignmentDeadlineDigest(EventEnvelope envelope)
    {
        var payload = envelope.DataAs<AssignmentDeadlineDigestEvent>();
        if (payload is null
            || payload.AdministratorUserId == Guid.Empty
            || string.IsNullOrWhiteSpace(payload.Title))
        {
            return null;
        }

        if (payload.NotStartedCount <= 0)
        {
            return null;
        }

        var title = payload.Title.Trim();
        var names = payload.NotStartedNames?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .ToList() ?? [];

        string notStarted;
        if (names.Count == 0)
        {
            notStarted = payload.NotStartedCount.ToString();
        }
        else if (names.Count < payload.NotStartedCount)
        {
            notStarted = $"{string.Join(", ", names)} и ещё {payload.NotStartedCount - names.Count}";
        }
        else
        {
            notStarted = string.Join(", ", names);
        }

        var body = $"«{title}» — срок до {payload.Deadline.ToString(DeadlineFormat)}. "
                   + $"Ещё не начали: {notStarted}. Откройте, чтобы напомнить в один клик.";

        return new CreateNotificationRequest(
            payload.AdministratorUserId,
            NotificationType.AssignmentDeadlineDigest,
            NotificationTitles.AssignmentDeadlineDigest,
            body,
            NotificationActionRoutes.AssignmentReminderPrompt(payload.AssignmentId),
            $"{payload.AssignmentId}:{payload.Deadline:O}",
            SendEmail: true);
    }

    /// <summary>
    /// Phase 40.26. A manager disputed a score. This is the notice 40.25 wrote the queue for and
    /// could not send — there was no way to enumerate an organization's administrators.
    ///
    /// <para>
    /// Dedupes on the note. One open dispute per conversation is a database constraint (40.25) and a
    /// note is never edited, so a second event with this key is a Kafka redelivery.
    /// </para>
    /// </summary>
    private static CreateNotificationRequest? MapDialogReviewDisputed(EventEnvelope envelope)
    {
        var payload = envelope.DataAs<DialogReviewDisputedEvent>();
        if (payload is null
            || payload.AdministratorUserId == Guid.Empty
            || string.IsNullOrWhiteSpace(payload.Comment))
        {
            return null;
        }

        var who = string.IsNullOrWhiteSpace(payload.SubjectDisplayName)
            ? "Менеджер"
            : payload.SubjectDisplayName.Trim();

        var body = payload.DisputedScore is { } disputedScore
            ? $"{who} не согласен с оценкой {disputedScore}: {Shorten(payload.Comment.Trim())}"
            : $"{who} не согласен с оценкой: {Shorten(payload.Comment.Trim())}";

        return new CreateNotificationRequest(
            payload.AdministratorUserId,
            NotificationType.DialogReviewDisputed,
            NotificationTitles.DialogReviewDisputed,
            body,
            NotificationActionRoutes.AdminDialogReview(payload.NoteId),
            payload.NoteId.ToString(),
            SendEmail: true);
    }

    /// <summary>
    /// Phase 40.25. The body opens with the quoted lines, because those lines are the whole reason
    /// the note exists — a notification announcing that feedback exists elsewhere is feedback
    /// nobody reads.
    ///
    /// <para>
    /// Dedupes on the note itself. A note is written once and never edited, so a second event with
    /// this key is a Kafka redelivery and collapsing it is correct.
    /// </para>
    /// </summary>
    private static CreateNotificationRequest? MapDialogReviewCommented(EventEnvelope envelope)
    {
        var payload = envelope.DataAs<DialogReviewCommentedEvent>();
        if (payload is null || payload.UserId == Guid.Empty || string.IsNullOrWhiteSpace(payload.Comment))
        {
            return null;
        }

        var quote = payload.QuotedText?.Trim();
        var body = string.IsNullOrEmpty(quote)
            ? payload.Comment.Trim()
            : $"«{Shorten(quote)}» — {payload.Comment.Trim()}";

        return new CreateNotificationRequest(
            payload.UserId,
            NotificationType.DialogReviewCommented,
            NotificationTitles.DialogReviewCommented,
            body,
            NotificationActionRoutes.DialogReview(payload.NoteId),
            payload.NoteId.ToString(),
            SendEmail: true);
    }

    /// <summary>
    /// Phase 40.25. Names the outcome in the first sentence. «Рассмотрено» on its own would be the
    /// same silence the dispute mechanism exists to replace.
    ///
    /// <para>
    /// Dedupes on the note: a dispute is ruled on once — the service refuses to re-resolve a closed
    /// row — so the note id alone is the whole key.
    /// </para>
    /// </summary>
    private static CreateNotificationRequest? MapDialogReviewResolved(EventEnvelope envelope)
    {
        var payload = envelope.DataAs<DialogReviewResolvedEvent>();
        if (payload is null || payload.UserId == Guid.Empty || string.IsNullOrWhiteSpace(payload.Outcome))
        {
            return null;
        }

        var upheld = string.Equals(payload.Outcome, UpheldOutcome, StringComparison.OrdinalIgnoreCase);
        var body = upheld
            ? payload.AdjustedScore is { } adjusted
                ? $"РОП согласился: оценка должна была быть {adjusted} вместо {payload.DisputedScore}."
                : "РОП согласился с вами: оценка была выставлена неверно."
            : "РОП посмотрел запись — оценка остаётся прежней.";

        var resolution = payload.Resolution?.Trim();
        if (!string.IsNullOrEmpty(resolution))
        {
            body += $" {resolution}";
        }

        return new CreateNotificationRequest(
            payload.UserId,
            NotificationType.DialogReviewResolved,
            NotificationTitles.DialogReviewResolved,
            body,
            NotificationActionRoutes.DialogReview(payload.NoteId),
            payload.NoteId.ToString(),
            SendEmail: true);
    }

    /// <summary>Keeps a quoted fragment to one readable line in an inbox and in an email body.</summary>
    private static string Shorten(string text)
        => text.Length <= PreviewMaximumLength
            ? text
            : text[..(PreviewMaximumLength - EllipsisReserveLength)].TrimEnd() + Ellipsis;

    /// <summary>
    /// Phase 39. <c>SendEmail</c> stays false: follow-up due reminders are in-app only per product
    /// spec.
    ///
    /// <para>
    /// Dedupes on company plus the specific due date, not just the company: company-service resets
    /// <c>FollowUpNotifiedAt</c> on reschedule, so a later due date for the same company must produce
    /// a fresh notification rather than being suppressed by the still-inboxed reminder for the
    /// earlier date. Uses the round-trip ("O") format so the key is exact to the tick and
    /// reproducible byte-for-byte on both the producer's original <c>DateTime</c> and any
    /// re-serialization here — a lossier format (seconds-only, say) could collapse two
    /// distinct-but-close due dates onto one key.
    /// </para>
    /// </summary>
    private static CreateNotificationRequest? MapCompanyFollowUpDue(EventEnvelope envelope)
    {
        var payload = envelope.DataAs<CompanyFollowUpDueEvent>();
        if (payload is null || payload.UserId == Guid.Empty || string.IsNullOrWhiteSpace(payload.CompanyName))
        {
            return null;
        }

        var body = string.IsNullOrWhiteSpace(payload.Note)
            ? "Настало время запланированного контакта."
            : payload.Note.Trim();

        return new CreateNotificationRequest(
            payload.UserId,
            NotificationType.CompanyFollowUpDue,
            NotificationTitles.CompanyFollowUpDue(payload.CompanyName),
            body,
            NotificationActionRoutes.CompanyDetails(payload.CompanyId),
            $"{payload.CompanyId}:{payload.NextActionAt:O}");
    }
}
