using Sellevate.Social.Features.Chat.Constants;
using Sellevate.Social.Features.Chat.Models;

namespace Sellevate.Social.Features.Chat.Services.Abstract;

/// <summary>
/// One-to-one chat between two people who are already accepted friends. That precondition is the
/// tenant boundary as much as it is a product rule: friendships cannot cross an organization, so a
/// conversation cannot either.
///
/// <para>
/// Every method here identifies the caller by <c>userId</c> and refuses a conversation the caller is
/// not a participant of, reporting it as not found rather than as forbidden.
/// </para>
/// </summary>
public interface IChatService
{
    /// <summary>
    /// The conversation between these two, created on first use. Throws when the two are not
    /// accepted friends.
    /// </summary>
    Task<ChatConversationSummaryDto> GetOrCreateConversationAsync(Guid userId, Guid friendUserId, CancellationToken cancellationToken = default);

    Task<ChatMessageDto> SendMessageAsync(Guid senderId, string conversationId, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of messages, oldest-first, ending before <paramref name="beforeMessageId"/> when one
    /// is supplied. <paramref name="limit"/> is clamped to the page-size bounds in
    /// <see cref="ChatConstants"/> rather than rejected.
    /// </summary>
    Task<List<ChatMessageDto>> GetMessagesAsync(Guid userId, string conversationId, int limit = ChatConstants.DefaultMessagePageSize, string? beforeMessageId = null, CancellationToken cancellationToken = default);

    Task<List<ChatConversationSummaryDto>> GetConversationListAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Moves this participant's read watermark to now, leaving the other's untouched.</summary>
    Task MarkConversationReadAsync(Guid userId, string conversationId, CancellationToken cancellationToken = default);
}
