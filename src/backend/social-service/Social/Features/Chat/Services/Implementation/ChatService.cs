using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;
using Sellevate.Social.Eventing;
using Sellevate.Social.Features.Chat.Models;
using Sellevate.Social.Features.Chat.Services.Abstract;
using Sellevate.Social.Features.Friends.Models;
using Sellevate.Social.Infrastructure.Data;
using Sellevate.Social.Infrastructure.Mongo;

namespace Sellevate.Social.Features.Chat.Services.Implementation;

internal sealed class ChatService(
    MongoDbContext mongoContext,
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
        await ValidateFriendshipExistsAsync(userId, friendUserId, cancellationToken);

        var sortedParticipantIds = BuildSortedParticipantIds(userId, friendUserId);

        var participantsFilter = Builders<ChatConversation>.Filter.Eq(
            conversation => conversation.ParticipantIds,
            sortedParticipantIds);

        var existingConversation = await mongoContext.ChatConversations
            .Find(participantsFilter)
            .FirstOrDefaultAsync(cancellationToken);

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

        await mongoContext.ChatConversations.InsertOneAsync(newConversation, cancellationToken: cancellationToken);

        var newFriendDisplayName = await GetUserDisplayNameAsync(friendUserId, cancellationToken);
        return MapToConversationSummary(newConversation, friendUserId, newFriendDisplayName);
    }

    public async Task<ChatMessageDto> SendMessageAsync(
        Guid senderId,
        string conversationId,
        string content,
        CancellationToken cancellationToken = default)
    {
        var conversation = await mongoContext.ChatConversations
            .Find(conversation => conversation.Id == conversationId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Conversation not found.");

        if (!conversation.ParticipantIds.Contains(senderId))
            throw new InvalidOperationException("You are not a participant in this conversation.");

        var chatMessage = new ChatMessage
        {
            Id = ObjectId.GenerateNewId().ToString(),
            SenderId = senderId,
            Content = content,
            SentAt = DateTime.UtcNow
        };

        var updateDefinition = Builders<ChatConversation>.Update
            .Push(conversation => conversation.Messages, chatMessage)
            .Set(conversation => conversation.LastMessageAt, chatMessage.SentAt);

        await mongoContext.ChatConversations.UpdateOneAsync(
            conversation => conversation.Id == conversationId,
            updateDefinition,
            cancellationToken: cancellationToken);

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
        var conversation = await mongoContext.ChatConversations
            .Find(conversation => conversation.Id == conversationId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Conversation not found.");

        if (!conversation.ParticipantIds.Contains(userId))
            throw new InvalidOperationException("You are not a participant in this conversation.");

        var messages = conversation.Messages;

        if (!string.IsNullOrEmpty(beforeMessageId))
        {
            var beforeIndex = messages.FindIndex(message => message.Id == beforeMessageId);
            if (beforeIndex > 0)
                messages = messages.Take(beforeIndex).ToList();
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
        var userConversations = await mongoContext.ChatConversations
            .Find(conversation => conversation.ParticipantIds.Contains(userId))
            .SortByDescending(conversation => conversation.LastMessageAt)
            .ToListAsync(cancellationToken);

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
