namespace Sellevate.Ai.Infrastructure.Configuration;

public sealed class OpenAiConfiguration
{
    public const string SectionName = "OpenAI";

    public required string ApiKey { get; init; }
    public string BaseUrl { get; init; } = "https://api.openai.com";
    public string ChatCompletionsPath { get; init; } = "/v1/chat/completions";
    public string OpenQuestionModel { get; init; } = "gpt-4.1";
    public string DialogModel { get; init; } = "gpt-4.1";
    public int MaximumOpenQuestionTokenCount { get; init; } = 300;
    public int MaximumDialogTokenCount { get; init; } = 500;
    public int MaximumFeedbackTokenCount { get; init; } = 1500;
    public double DialogTemperature { get; init; } = 0.7;
}
