using System.Text.Json;

namespace Sellevate.Ai.Features.Quotas.Services.Implementation;

/// <summary>
/// Phase 40.33. Reads the OpenAI-shaped <c>usage</c> block off a completion body.
///
/// <para>
/// Shared rather than duplicated because ai-service has two independent provider call paths —
/// <c>OpenAiChatService</c> and <c>AiEvaluationStrategyBase</c> — and a meter that read tokens
/// slightly differently on each would report a spend nobody could reconcile with the invoice. The
/// two paths are not merged in this block for a reason recorded in <c>docs/DECISIONS.md</c>: the
/// grading path has its own failure contract (<c>HttpRequestException</c>, degrade-to-failed-result)
/// that <c>LLM_FAILURE_HANDLING.md</c> pins and tests assert.
/// </para>
/// </summary>
internal static class OpenAiUsageReader
{
    public static AiCompletionUsage? Read(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var promptTokens = ReadTokenCount(usage, "prompt_tokens") ?? ReadTokenCount(usage, "promptTokens");
        var completionTokens = ReadTokenCount(usage, "completion_tokens") ?? ReadTokenCount(usage, "completionTokens");

        if (promptTokens is null && completionTokens is null)
        {
            return null;
        }

        return new AiCompletionUsage(promptTokens ?? 0, completionTokens ?? 0);
    }

    public static AiCompletionUsage? Read(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return Read(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? ReadTokenCount(JsonElement usage, string propertyName) =>
        usage.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var count) ? count : null;
}

internal sealed record AiCompletionUsage(int PromptTokens, int CompletionTokens);
