namespace Sellevate.Social.Features.Chat.Constants;

/// <summary>
/// The page sizes and preview lengths chat is tuned with. Shared between the controller's query
/// default and the service's clamp so a request that omits <c>limit</c> and a request that asks for
/// more than the maximum both land on a number the other half agrees with.
///
/// <para>
/// The two preview lengths are deliberately different: the conversation list shows a one-line
/// snippet inside the application, while the notification preview is the body of a push or an email
/// and has room for a sentence.
/// </para>
/// </summary>
internal static class ChatConstants
{
    /// <summary>Messages returned when a caller asks for no page size.</summary>
    public const int DefaultMessagePageSize = 50;

    /// <summary>Smallest page a caller may ask for; anything lower is clamped up.</summary>
    public const int MinimumMessagePageSize = 1;

    /// <summary>Largest page a caller may ask for; anything higher is clamped down.</summary>
    public const int MaximumMessagePageSize = 100;

    /// <summary>Characters of the last message kept for a conversation-list snippet.</summary>
    public const int ConversationPreviewLength = 100;

    /// <summary>Characters of a message body carried in the notification event.</summary>
    public const int NotificationPreviewLength = 160;

    /// <summary>Appended to a preview that was cut short.</summary>
    public const string PreviewEllipsis = "...";

    /// <summary>
    /// Stands in for a participant whose <c>UserReplica</c> has not arrived yet — the projection is
    /// eventually consistent, so a brand-new colleague can be in a conversation before their name is.
    /// </summary>
    public const string UnknownDisplayName = "Unknown";
}
