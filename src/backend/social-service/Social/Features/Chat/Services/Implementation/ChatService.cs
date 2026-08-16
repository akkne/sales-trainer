using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using Sellevate.Social.Eventing;
using Sellevate.Social.Features.Chat.Models;
using Sellevate.Social.Features.Chat.Services.Abstract;
using Sellevate.Social.Features.Friends.Models;
using Sellevate.Social.Infrastructure.Data;

namespace Sellevate.Social.Features.Chat.Services.Implementation;

/// <summary>
/// Phase 40.13 moved every Mongo call in this class behind
/// <see cref="IChatConversationRepository"/>. The organization is no longer something this service
/// reasons about: it cannot construct an unscoped query, because it no longer holds a collection.
/// </summary>
internal sealed class ChatService(
    IChatConversationRepository conversations,
    SocialDbContext databaseContext,
    ISocialEventPublisher eventPublisher) : IChatService
{
    private const int MaximumMessagePreviewLength = 100;
    private const int MaximumPreviewLength = 160;
    private const string UnknownDisplayName = "Unknown";

    public async Task<ChatConversationSummaryDto> GetOrCreateConversationAsync(
        Guid userId,
        Guid friendUserId,
        CancellationToken cancellationToken = default)
    {
        // The only method here that reads a table with a row-level-security policy (Friendships).
        // Without the transaction the SET LOCAL never applies, the friendship check returns zero
        // rows and every attempt to open a chat fails with "you can only chat with accepted
        // friends" — the fail-closed direction, but still a broken feature. The other methods read
        // UserReplicas only, which carries no policy (docs/TENANCY/TENANCY.md §4.2), and their
        // tenant boundary is the Mongo repository rather than Postgres.
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

    public async Task<ChatMessageDto> SendMessageAsync(
        Guid senderId,
        string conversationId,
        string content,
        CancellationToken cancellationToken = default)
    {
        // The repository's filter already carries both the organization and "senderId is a
        // participant", so a conversation in another organization and a conversation the sender is
        // not in are the same not-found here — deliberately, since telling them apart would confirm
        // that a given conversation id exists somewhere else.
        var conversation = await conversations.FindForParticipantAsync(conversationId, senderId, cancellationToken)
            ?? throw new KeyNotFoundException("Conversation not found.");

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

    public async Task<List<ChatMessageDto>> GetMessagesAsync(
        Guid userId,
        string conversationId,
        int limit = 50,
        string? beforeMessageId = null,
        CancellationToken cancellationToken = default)
    {
        // TODO SO3: restructure ChatConversation to a separate messages collection to avoid loading the full document here.
        limit = Math.Clamp(limit, 1, 100);

        var conversation = await conversations.FindForParticipantAsync(conversationId, userId, cancellationToken)
            ?? throw new KeyNotFoundException("Conversation not found.");

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
                var friendDisplayName = participantDisplayNames.GetValueOrDefault(friendUserId, UnknownDisplayName);
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
            ?? throw new KeyNotFoundException("Conversation not found.");

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

        var preview = content.Length > MaximumPreviewLength
            ? content[..MaximumPreviewLength] + "..."
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
            throw new InvalidOperationException("You can only chat with accepted friends.");
    }

    private async Task<string> GetUserDisplayNameAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var displayName = await databaseContext.UserReplicas
            .Where(replica => replica.UserId == userId)
            .Select(replica => replica.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(displayName) ? UnknownDisplayName : displayName;
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

        if (lastMessagePreview is not null && lastMessagePreview.Length > MaximumMessagePreviewLength)
            lastMessagePreview = lastMessagePreview[..MaximumMessagePreviewLength] + "...";

        return new ChatConversationSummaryDto(
            conversation.Id,
            friendUserId,
            friendDisplayName,
            lastMessagePreview,
            conversation.LastMessageAt);
    }
}
