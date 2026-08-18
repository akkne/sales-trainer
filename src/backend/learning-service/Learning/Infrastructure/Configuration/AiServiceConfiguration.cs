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

    /// <summary>Phase 40.32. One exercise rewritten into the organization's product and voice.</summary>
    public string ContentRewritePath { get; init; } = "/ai/content/rewrite";

    /// <summary>Phase 40.32. What is methodically wrong with one exercise a human wrote.</summary>
    public string ContentReviewPath { get; init; } = "/ai/content/review";

    /// <summary>
    /// Phase 40.33. The generic completion this service used to make itself, for the interactive
    /// <c>ai_dialogue</c> exercise.
    /// </summary>
    public string ChatPath { get; init; } = "/ai/chat";

    /// <summary>Phase 40.33. The same completion, streamed as newline-delimited deltas.</summary>
    public string ChatStreamPath { get; init; } = "/ai/chat/stream";

    /// <summary>Phase 40.33. Speech synthesis, for the same exercise's voice mode.</summary>
    public string TextToSpeechPath { get; init; } = "/ai/tts";

    /// <summary>
    /// Phase 40.33. Outer bound on one interactive chat or synthesis call. Ninety seconds is what the
    /// removed in-process client used, so a stalled provider degrades on the same clock it always
    /// did — Polly's per-attempt budget now lives in ai-service, on the other side of this hop.
    /// </summary>
    public int ChatTimeoutSeconds { get; init; } = 90;

    /// <summary>
    /// Phase 40.27. How long one pipeline call may take. The default <see cref="HttpClient"/> timeout
    /// is 100 seconds and generating a lesson routinely exceeds it — a timeout there would abandon a
    /// call the provider has already been paid for, and the retry would pay again.
    /// </summary>
    public int ContentPipelineTimeoutSeconds { get; init; } = 300;
}
