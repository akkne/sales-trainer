using Sellevate.Ai.Features.Dialog.Models;

namespace Sellevate.Ai.Features.Dialog.Services.Abstract;

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
        DialogXpWeights xpWeights,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One-shot free-text completion: no JSON response-format contract, no XP/summary
    /// post-processing. Used by non-dialogue features (e.g. company briefing generation) that
    /// need a plain-text/markdown answer to a single system+user prompt pair.
    /// </summary>
    Task<string> GenerateTextAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);
}
