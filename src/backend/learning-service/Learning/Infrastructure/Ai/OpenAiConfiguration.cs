namespace Sellevate.Learning.Infrastructure.Ai;

public sealed class OpenAiConfiguration
{
    public const string SectionName = "OpenAI";

    public required string ApiKey { get; init; }
    public string BaseUrl { get; init; } = "https://api.openai.com";
    public string ChatCompletionsPath { get; init; } = "/v1/chat/completions";
    public string DialogModel { get; init; } = "gpt-4.1";
    public int MaximumDialogTokenCount { get; init; } = 500;
    public double DialogTemperature { get; init; } = 0.7;
}
