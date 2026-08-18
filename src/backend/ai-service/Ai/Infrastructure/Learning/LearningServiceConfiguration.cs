namespace Sellevate.Ai.Infrastructure.Learning;

/// <summary>
/// Where learning-service lives and how long ai-service is willing to wait for it.
/// </summary>
public sealed class LearningServiceConfiguration
{
    public const string SectionName = "LearningService";

    public required string BaseUrl { get; init; }

    public string PracticeContextPath { get; init; } = "/internal/assignments/practice-context";

    /// <summary>
    /// Short, because this call sits in front of a learner pressing "start". The lookup degrades to
    /// "no assignment" on timeout, so a slow learning-service costs an un-personalised practice
    /// conversation rather than a practice screen that will not open.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 5;
}
