using SalesTrainer.Api.Features.Dialog.Models;

namespace SalesTrainer.Api.Features.Dialog.Services.Abstract;

public interface IOpenAiChatService
{
    bool IsConfigured { get; }

    Task<ChatMessageResult> SendChatMessageAsync(
        string systemPrompt,
        List<DialogMessage> conversationHistory,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamChatMessageAsync(
        string systemPrompt,
        List<DialogMessage> conversationHistory,
        CancellationToken cancellationToken = default);

    Task<FeedbackResult> GenerateFeedbackAsync(
        string feedbackPrompt,
        List<DialogMessage> conversationHistory,
        CancellationToken cancellationToken = default);
}
