namespace Sellevate.Learning.Features.ContentGeneration;

/// <summary>Phase 40.27. Tuning for the admin content pipeline's background half.</summary>
public sealed class ContentGenerationOptions
{
    public const string SectionName = "ContentGeneration";

    /// <summary>
    /// How often the sweep looks for work. Short, because an administrator is watching a spinner —
    /// unlike the deadline and repeat sweeps, whose subject is a date days away.
    /// </summary>
    public int SweepIntervalSeconds { get; init; } = 20;

    /// <summary>
    /// How long a claim is honoured before another tick may take the run back. Longer than the
    /// pipeline's HTTP timeout on purpose: a lease that expires while the call is still in flight
    /// would pay for the same generation twice.
    /// </summary>
    public int ClaimLeaseMinutes { get; init; } = 10;

    /// <summary>
    /// Attempts per half before the run is marked failed and waits for a person. Three, because the
    /// failures worth retrying here (a truncated completion, a provider hiccup) are transient and the
    /// ones that are not — a deck the model cannot make sense of — do not improve on the fourth try.
    /// </summary>
    public int MaximumAttempts { get; init; } = 3;

    /// <summary>
    /// Runs advanced per organization per tick. One at a time keeps a customer who queued ten decks
    /// from holding the sweep for every other customer.
    /// </summary>
    public int MaximumJobsPerTick { get; init; } = 2;

    /// <summary>
    /// Upper bound on generated exercises, never a target. «Лучше 4 хороших упражнения, чем 15
    /// ватных» — the sentence is 40.28's, the ceiling belongs here already.
    /// </summary>
    public int MaximumExercisesPerLesson { get; init; } = 8;

    /// <summary>
    /// The iconic name of the per-organization skill generated lessons are filed under. One skill per
    /// organization, created on first use: a generated lesson needs a topic and a topic needs a
    /// skill, and inventing a skill per run would fill the tree with one-lesson skills.
    /// </summary>
    public string GeneratedSkillIconicName { get; init; } = "ai-generated";

    public string GeneratedSkillTitle { get; init; } = "Материалы компании";
}
