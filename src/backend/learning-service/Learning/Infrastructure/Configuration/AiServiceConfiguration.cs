namespace Sellevate.Learning.Infrastructure.Configuration;

public sealed class AiServiceConfiguration
{
    public const string SectionName = "AiService";

    public required string BaseUrl { get; init; }

    public string EvaluatePath { get; init; } = "/ai/evaluate";

    /// <summary>Phase 40.27. The first half of the admin content pipeline: material in, structure out.</summary>
    public string ContentStructurePath { get; init; } = "/ai/content/structure";

    /// <summary>Phase 40.27. The second half, run only after a human has confirmed the structure.</summary>
    public string ContentGeneratePath { get; init; } = "/ai/content/generate";

    /// <summary>
    /// Phase 40.27. How long one pipeline call may take. The default <see cref="HttpClient"/> timeout
    /// is 100 seconds and generating a lesson routinely exceeds it — a timeout there would abandon a
    /// call the provider has already been paid for, and the retry would pay again.
    /// </summary>
    public int ContentPipelineTimeoutSeconds { get; init; } = 300;
}
