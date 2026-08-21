namespace Sellevate.Ai.Infrastructure.Learning;

/// <summary>
/// Where learning-service lives and how long ai-service is willing to wait for it.
/// </summary>
public sealed class LearningServiceConfiguration
{
    public const string SectionName = "LearningService";

    public required string BaseUrl { get; init; }

    public string PracticeContextPath { get; init; } = "/internal/assignments/practice-context";

    /// <summary>C-3 audit fix. Where the global skill catalog is read from, for labelling dialog bundles.</summary>
    public string SkillLookupPath { get; init; } = "/internal/skills/lookup";

    /// <summary>
    /// Short, because this call sits in front of a learner pressing "start". The lookup degrades to
    /// "no assignment" on timeout, so a slow learning-service costs an un-personalised practice
    /// conversation rather than a practice screen that will not open.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 5;

    /// <summary>
    /// R-15 audit fix. How long <see cref="SkillCatalogCache"/> keeps the skill catalog before
    /// re-fetching it. Skills are global, rarely-changing content, so minutes rather than seconds is
    /// the right order of magnitude — a title edit in the admin skill editor does not need to be
    /// reflected in a dialog-bundle label within the same request.
    /// </summary>
    public int SkillCatalogCacheMinutes { get; init; } = 5;
}
