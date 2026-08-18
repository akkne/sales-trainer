namespace Sellevate.Company.Features.Companies;

/// <summary>
/// Tuning for the company CRUD and AI-backed reads, bound from the <c>Companies</c> config section.
/// Every value here can be changed without a rebuild and without a migration; nothing here changes
/// what a response <i>means</i>, only how much of it there is or how long a cached "not yet" is
/// trusted.
///
/// <para>
/// Values that look similar but are deliberately <b>not</b> here: the cap on session ids sent to
/// ai-service mirrors that service's own hard guard, so it is a contract and lives as a constant;
/// column lengths are schema and live in <c>CompanyFieldLengths</c>.
/// </para>
/// </summary>
public sealed class CompanyServiceOptions
{
    public const string SectionName = "Companies";

    /// <summary>
    /// How much of a company's description the list view carries. The list is a summary, so this
    /// only bounds the excerpt — the full description is always available on the detail read.
    /// </summary>
    public int DescriptionExcerptLength { get; set; } = 160;

    /// <summary>
    /// How many distinct recent practice goals the goal picker offers, most recently used first.
    /// </summary>
    public int RecentGoalCount { get; set; } = 5;

    /// <summary>
    /// How many of the newest call-log entries are fed to the briefing prompt. Raising it costs
    /// prompt tokens on every briefing generation.
    /// </summary>
    public int RecentCallLogCountForBriefing { get; set; } = 5;

    /// <summary>
    /// How long a "no usable feedback yet" readiness result is trusted before the fan-out over the
    /// company's practice sessions is retried. Short on purpose: feedback can land at any moment,
    /// and the eager invalidation on a new practice call only covers the signal it can see.
    /// </summary>
    public int ReadinessNoFeedbackCacheMinutes { get; set; } = 2;
}
