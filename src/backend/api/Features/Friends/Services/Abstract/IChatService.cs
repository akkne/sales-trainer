using SalesTrainer.Api.Features.Friends.Models;

namespace SalesTrainer.Api.Features.Friends.Services.Abstract;

public interface IChatService
{
    Task<ChatConversationSummaryDto> GetOrCreateConversationAsync(Guid userId, Guid friendUserId, CancellationToken cancellationToken = default);
    Task<ChatMessageDto> SendMessageAsync(Guid senderId, string conversationId, string content, CancellationToken cancellationToken = default);
    Task<List<ChatMessageDto>> GetMessagesAsync(Guid userId, string conversationId, int limit = 50, string? beforeMessageId = null, CancellationToken cancellationToken = default);
    Task<List<ChatConversationSummaryDto>> GetConversationListAsync(Guid userId, CancellationToken cancellationToken = default);
}
