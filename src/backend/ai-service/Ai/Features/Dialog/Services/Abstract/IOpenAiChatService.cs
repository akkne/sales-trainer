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
    /// <param name="model">
    /// Overrides the model used for this call. Defaults to <see cref="Sellevate.Ai.Infrastructure.Configuration.OpenAiConfiguration.OpenQuestionModel"/> when null.
    /// </param>
    /// <param name="maxTokens">
    /// Overrides the max token count for this call. Defaults to <see cref="Sellevate.Ai.Infrastructure.Configuration.OpenAiConfiguration.MaximumFeedbackTokenCount"/> when null.
    /// </param>
    Task<string> GenerateTextAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default,
        string? model = null,
        int? maxTokens = null);
}
