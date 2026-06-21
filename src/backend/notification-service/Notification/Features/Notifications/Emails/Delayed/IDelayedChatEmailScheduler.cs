namespace Sellevate.Notification.Features.Notifications.Emails.Delayed;

/// <summary>
/// Schedules and resolves the "email me about an unread message" flow. A message received now is
/// scheduled for an email after the grace period; a read receipt records a watermark that
/// suppresses any still-pending email for that conversation up to the read time.
/// </summary>
public interface IDelayedChatEmailScheduler
{
    /// <summary>Schedule an unread-message email for delivery after the grace period.</summary>
    Task ScheduleAsync(PendingChatEmail pending, CancellationToken cancellationToken = default);

    /// <summary>Record that <paramref name="readerUserId"/> read <paramref name="conversationId"/>
    /// up to <paramref name="readAt"/>, suppressing pending emails for messages sent at or before it.</summary>
    Task MarkConversationReadAsync(
        Guid readerUserId, Guid? conversationId, DateTime readAt, CancellationToken cancellationToken = default);

    /// <summary>Atomically claim up to <paramref name="maxItems"/> pending emails whose grace period
    /// has elapsed by <paramref name="asOf"/>. Claimed items are removed from the queue.</summary>
    Task<IReadOnlyList<PendingChatEmail>> ClaimDueAsync(
        DateTime asOf, int maxItems, CancellationToken cancellationToken = default);

    /// <summary>True if the conversation was read at or after the message was sent (so no email is due).</summary>
    Task<bool> WasReadAsync(PendingChatEmail pending, CancellationToken cancellationToken = default);
}
