namespace Sellevate.Ai.Infrastructure.Configuration;

/// <summary>
/// Identifies which OpenAI-compatible provider is in use so that
/// provider-specific headers and response-format schemas are selected
/// without magic-string URL sniffing.
/// </summary>
public enum OpenAiProvider
{
    /// <summary>Standard OpenAI (api.openai.com) — uses Bearer token and json_schema wrapper.</summary>
    OpenAi,

    /// <summary>F5 AI gateway — uses X-Auth-Token and a flat json_schema object.</summary>
    F5Ai,
}

public sealed class OpenAiConfiguration
{
    public const string SectionName = "OpenAI";

    public required string ApiKey { get; init; }
    public string BaseUrl { get; init; } = "https://api.openai.com";
    public string ChatCompletionsPath { get; init; } = "/v1/chat/completions";

    /// <summary>
    /// Explicit provider selection. Defaults to <see cref="OpenAiProvider.OpenAi"/>.
    /// Set to <c>F5Ai</c> when routing through the F5 AI gateway.
    /// </summary>
    public OpenAiProvider Provider { get; init; } = OpenAiProvider.OpenAi;

    public string OpenQuestionModel { get; init; } = "gpt-4.1";
    public string DialogModel { get; init; } = "gpt-4.1";
    public int MaximumOpenQuestionTokenCount { get; init; } = 300;
    public int MaximumDialogTokenCount { get; init; } = 500;
    public int MaximumFeedbackTokenCount { get; init; } = 1500;
    public double DialogTemperature { get; init; } = 0.7;
}
