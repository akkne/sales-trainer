using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using Sellevate.Social.Common.Constants;
using Sellevate.Social.Eventing;
using Sellevate.Social.Features.Chat.Constants;
using Sellevate.Social.Features.Chat.Models;
using Sellevate.Social.Features.Chat.Services.Abstract;
using Sellevate.Social.Features.Friends.Models;
using Sellevate.Social.Infrastructure.Data;

namespace Sellevate.Social.Features.Chat.Services.Implementation;

/// <summary>
/// Phase 40.13 moved every Mongo call in this class behind
/// <see cref="IChatConversationRepository"/>. The organization is no longer something this service
/// reasons about: it cannot construct an unscoped query, because it no longer holds a collection.
///
/// <para>
/// Only <see cref="GetOrCreateConversationAsync"/> opens a <see cref="TenantTransactionScope"/>,
/// because it is the only method that reads a table carrying a row-level-security policy
/// (<c>Friendships</c>). Every other method reads <c>UserReplicas</c>, which has no policy
/// (docs/TENANCY/TENANCY.md §4.2), and takes its tenant boundary from the Mongo repository instead.
/// </para>
/// </summary>
internal sealed class ChatService(
    IChatConversationRepository conversations,
    SocialDbContext databaseContext,
    ISocialEventPublisher eventPublisher) : IChatService
{
    /// <summary>
    /// Opens the conversation between two accepted friends, creating it on first use. Without the
    /// transaction the <c>SET LOCAL</c> never applies, the friendship check sees zero rows, and every
    /// attempt to open a chat is refused as "not friends" — fail-closed, but a broken screen.
    /// </summary>
    public async Task<ChatConversationSummaryDto> GetOrCreateConversationAsync(
        Guid userId,
        Guid friendUserId,
        CancellationToken cancellationToken = default)
    {
        await using var scope = await TenantTransactionScope.BeginReadAsync(databaseContext, cancellationToken);

        await ValidateFriendshipExistsAsync(userId, friendUserId, cancellationToken);

        var sortedParticipantIds = BuildSortedParticipantIds(userId, friendUserId);

        var existingConversation = await conversations.FindByParticipantsAsync(
            sortedParticipantIds, cancellationToken);

        if (existingConversation is not null)
        {
            var friendDisplayName = await GetUserDisplayNameAsync(friendUserId, cancellationToken);
            return MapToConversationSummary(existingConversation, friendUserId, friendDisplayName);
        }

        var newConversation = new ChatConversation
        {
            ParticipantIds = sortedParticipantIds,
            CreatedAt = DateTime.UtcNow
        };

        await conversations.InsertAsync(newConversation, cancellationToken);

        var newFriendDisplayName = await GetUserDisplayNameAsync(friendUserId, cancellationToken);
        return MapToConversationSummary(newConversation, friendUserId, newFriendDisplayName);
    }

    /// <summary>
    /// Appends a message on behalf of a participant. The repository's filter carries both the
    /// organization and "the sender is a participant", so a conversation in another organization and
    /// a conversation the sender is not in fail identically — telling them apart would confirm that
    /// a given conversation id exists somewhere else.
    /// </summary>
    public async Task<ChatMessageDto> SendMessageAsync(
        Guid senderId,
        string conversationId,
        string content,
        CancellationToken cancellationToken = default)
    {
        var conversation = await conversations.FindForParticipantAsync(conversationId, senderId, cancellationToken)
            ?? throw new KeyNotFoundException(ErrorMessages.ConversationNotFound);

        var chatMessage = new ChatMessage
        {
            Id = ObjectId.GenerateNewId().ToString(),
            SenderId = senderId,
            Content = content,
            SentAt = DateTime.UtcNow
        };

        await conversations.AppendMessageAsync(conversationId, senderId, chatMessage, cancellationToken);

        await PublishChatMessageSentAsync(conversation, senderId, content, cancellationToken);

        return new ChatMessageDto(
            chatMessage.Id,
            chatMessage.SenderId,
            chatMessage.Content,
            chatMessage.SentAt,
            true);
    }

    /// <summary>
    /// One page of messages, oldest-first within the page, ending just before
    /// <paramref name="beforeMessageId"/> when one is given. An unknown
    /// <paramref name="beforeMessageId"/> yields an empty page rather than the newest messages, so a
    /// stale cursor cannot make the client jump back to the bottom.
    ///
    /// <para>
    /// Messages are embedded in the conversation document, so paging happens in memory over the whole
    /// document — see docs/DECISIONS.md on the pending move to a separate messages collection.
    /// </para>
    /// </summary>
    public async Task<List<ChatMessageDto>> GetMessagesAsync(
        Guid userId,
        string conversationId,
        int limit = ChatConstants.DefaultMessagePageSize,
        string? beforeMessageId = null,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(
            limit, ChatConstants.MinimumMessagePageSize, ChatConstants.MaximumMessagePageSize);

        var conversation = await conversations.FindForParticipantAsync(conversationId, userId, cancellationToken)
            ?? throw new KeyNotFoundException(ErrorMessages.ConversationNotFound);

        var messages = conversation.Messages;

        if (!string.IsNullOrEmpty(beforeMessageId))
        {
            var beforeIndex = messages.FindIndex(message => message.Id == beforeMessageId);
            messages = beforeIndex < 0 ? [] : messages.Take(beforeIndex).ToList();
        }

        return messages
            .OrderByDescending(message => message.SentAt)
            .Take(limit)
            .OrderBy(message => message.SentAt)
            .Select(message => new ChatMessageDto(
                message.Id,
                message.SenderId,
                message.Content,
                message.SentAt,
                message.SenderId == userId))
            .ToList();
    }

    public async Task<List<ChatConversationSummaryDto>> GetConversationListAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var userConversations = await conversations.ListForParticipantAsync(userId, cancellationToken);

        if (userConversations.Count == 0)
            return [];

        var allParticipantIds = userConversations
            .SelectMany(conversation => conversation.ParticipantIds)
            .Where(participantId => participantId != userId)
            .Distinct()
            .ToList();

        var participantDisplayNames = await databaseContext.UserReplicas
            .Where(replica => allParticipantIds.Contains(replica.UserId))
            .ToDictionaryAsync(replica => replica.UserId, replica => replica.DisplayName, cancellationToken);

        return userConversations
            .Select(conversation =>
            {
                var friendUserId = conversation.ParticipantIds.First(participantId => participantId != userId);
                var friendDisplayName = participantDisplayNames.GetValueOrDefault(
                    friendUserId, ChatConstants.UnknownDisplayName);
                return MapToConversationSummary(conversation, friendUserId, friendDisplayName);
            })
            .ToList();
    }

    public async Task MarkConversationReadAsync(
        Guid userId,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await conversations.FindForParticipantAsync(conversationId, userId, cancellationToken)
            ?? throw new KeyNotFoundException(ErrorMessages.ConversationNotFound);

        var readAt = DateTime.UtcNow;

        await conversations.SetReadWatermarkAsync(conversationId, userId, readAt, cancellationToken);

        await eventPublisher.PublishChatMessageReadAsync(
            new ChatMessageReadEvent(userId, ParseConversationId(conversation.Id), readAt),
            cancellationToken);
    }

    private async Task PublishChatMessageSentAsync(
        ChatConversation conversation,
        Guid senderId,
        string content,
        CancellationToken cancellationToken)
    {
        var recipientUserId = conversation.ParticipantIds
            .FirstOrDefault(participantId => participantId != senderId);

        if (recipientUserId == Guid.Empty) return;

        var senderDisplayName = await GetUserDisplayNameAsync(senderId, cancellationToken);

        var preview = content.Length > ChatConstants.NotificationPreviewLength
            ? content[..ChatConstants.NotificationPreviewLength] + ChatConstants.PreviewEllipsis
            : content;

        await eventPublisher.PublishChatMessageSentAsync(
            new ChatMessageSentEvent(
                recipientUserId,
                senderDisplayName,
                preview,
                ParseConversationId(conversation.Id)),
            cancellationToken);
    }

    private async Task ValidateFriendshipExistsAsync(
        Guid userId,
        Guid friendUserId,
        CancellationToken cancellationToken)
    {
        var friendshipExists = await databaseContext.Friendships
            .AnyAsync(friendship =>
                friendship.Status == FriendshipStatus.Accepted &&
                ((friendship.RequesterId == userId && friendship.AddresseeId == friendUserId) ||
                 (friendship.RequesterId == friendUserId && friendship.AddresseeId == userId)),
                cancellationToken);

        if (!friendshipExists)
            throw new InvalidOperationException(ErrorMessages.ChatRequiresAcceptedFriendship);
    }

    private async Task<string> GetUserDisplayNameAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var displayName = await databaseContext.UserReplicas
            .Where(replica => replica.UserId == userId)
            .Select(replica => replica.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(displayName) ? ChatConstants.UnknownDisplayName : displayName;
    }

    private static Guid? ParseConversationId(string conversationId) =>
        Guid.TryParse(conversationId, out var parsed) ? parsed : null;

    private static List<Guid> BuildSortedParticipantIds(Guid userId, Guid friendUserId)
    {
        var participantIds = new List<Guid> { userId, friendUserId };
        participantIds.Sort();
        return participantIds;
    }

    private static ChatConversationSummaryDto MapToConversationSummary(
        ChatConversation conversation,
        Guid friendUserId,
        string friendDisplayName)
    {
        var lastMessage = conversation.Messages.LastOrDefault();
        var lastMessagePreview = lastMessage?.Content;

        if (lastMessagePreview is not null && lastMessagePreview.Length > ChatConstants.ConversationPreviewLength)
            lastMessagePreview =
                lastMessagePreview[..ChatConstants.ConversationPreviewLength] + ChatConstants.PreviewEllipsis;

        return new ChatConversationSummaryDto(
            conversation.Id,
            friendUserId,
            friendDisplayName,
            lastMessagePreview,
            conversation.LastMessageAt);
    }
}
