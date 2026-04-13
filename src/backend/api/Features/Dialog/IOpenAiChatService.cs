namespace SalesTrainer.Api.Features.Dialog;

public interface IOpenAiChatService
{
    bool IsConfigured { get; }

    Task<ChatMessageResult> SendChatMessageAsync(
        string systemPrompt,
        List<DialogMessage> conversationHistory,
        CancellationToken cancellationToken = default);

    Task<FeedbackResult> GenerateFeedbackAsync(
        string feedbackPrompt,
        List<DialogMessage> conversationHistory,
        CancellationToken cancellationToken = default);
}
