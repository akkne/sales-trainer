namespace Sellevate.Learning.Infrastructure.Configuration;

public sealed class AiServiceConfiguration
{
    public const string SectionName = "AiService";

    public required string BaseUrl { get; init; }

    public string EvaluatePath { get; init; } = "/ai/evaluate";
}
