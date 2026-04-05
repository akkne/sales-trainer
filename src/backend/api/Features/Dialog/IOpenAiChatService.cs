namespace SalesTrainer.Api.Features.Dialog;

public interface IOpenAiChatService
{
    bool IsConfigured { get; }
    Task<ChatMessageResult> SendChatMessageAsync(string systemPrompt, List<DialogMessage> conversationHistory);
    Task<FeedbackResult> GenerateFeedbackAsync(string feedbackPrompt, List<DialogMessage> conversationHistory);
}

public class ChatMessageResult
{
    public string Content { get; set; } = null!;
    public bool IsStopSignal { get; set; }
}

public class FeedbackResult
{
    public string Content { get; set; } = null!;
    public int XpReward { get; set; }
}
