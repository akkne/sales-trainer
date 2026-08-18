using Sellevate.Social.Features.Chat.Models;

namespace Sellevate.Social.Features.Chat.Services.Abstract;

/// <summary>
/// Phase 40.13. The only way into the <c>chat_conversations</c> collection.
///
/// <para>
/// Mongo has no row-level security, so the boundary between two customers' chats is application
/// code. That is only trustworthy if there is one piece of it, which is why this interface exists
/// at all rather than <c>ChatService</c> holding a collection handle: the implementation creates
/// the collection handle itself, no other file may name it, and a unit test asserts that against
/// the source tree.
/// </para>
///
/// <para>
/// No method takes an organization and none returns conversations across organizations. The tenant
/// comes from <c>ITenantContext</c>, and an unset tenant raises — the Mongo equivalent of an RLS
/// policy failing closed, and the only thing playing that role here.
/// </para>
/// </summary>
public interface IChatConversationRepository
{
    /// <summary>The conversation between exactly these two participants, or <see langword="null"/>.</summary>
    Task<ChatConversation?> FindByParticipantsAsync(
        IReadOnlyList<Guid> sortedParticipantIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The conversation with this id, but only if it belongs to the current organization AND
    /// <paramref name="participantUserId"/> is in it. Both halves are in the filter rather than
    /// checked afterwards by the caller: a "load, then verify" shape is one forgotten check away
    /// from returning somebody else's messages.
    /// </summary>
    Task<ChatConversation?> FindForParticipantAsync(
        string conversationId,
        Guid participantUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Every conversation this user takes part in, newest activity first.</summary>
    Task<List<ChatConversation>> ListForParticipantAsync(
        Guid participantUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a conversation, stamping it with the current organization.</summary>
    Task InsertAsync(ChatConversation conversation, CancellationToken cancellationToken = default);

    /// <summary>Appends a message, and only to a conversation this participant belongs to.</summary>
    Task AppendMessageAsync(
        string conversationId,
        Guid senderId,
        ChatMessage message,
        CancellationToken cancellationToken = default);

    /// <summary>Moves this participant's read watermark, leaving the other participant's alone.</summary>
    Task SetReadWatermarkAsync(
        string conversationId,
        Guid participantUserId,
        DateTime readAt,
        CancellationToken cancellationToken = default);
}
