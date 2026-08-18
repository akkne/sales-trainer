namespace Sellevate.Learning.Features.ContentAdaptation;

/// <summary>Phase 40.32. Tuning for the batch adaptation worker.</summary>
public sealed class ContentAdaptationOptions
{
    public const string SectionName = "ContentAdaptation";

    /// <summary>
    /// How often the sweep looks for work. As short as 40.27's, and for the same reason: an
    /// administrator started this by pressing a button and is watching a progress bar.
    /// </summary>
    public int SweepIntervalSeconds { get; init; } = 20;

    /// <summary>
    /// How long a claim on a batch is honoured before another tick may take it back. Longer than the
    /// per-exercise HTTP timeout by a wide margin — a lease that expires mid-call would let a second
    /// tick re-read exercises the first one has already paid for.
    /// </summary>
    public int ClaimLeaseMinutes { get; init; } = 10;

    /// <summary>
    /// Attempts per <b>item</b>, not per batch. Budgeting per item is what stops one exercise the
    /// model keeps choking on from consuming the whole batch's budget and failing fifty proposals
    /// that would have been fine.
    /// </summary>
    public int MaximumAttemptsPerItem { get; init; } = 2;

    /// <summary>
    /// Exercises answered per batch per tick. Small on purpose: it bounds how much money one tick can
    /// spend, and it is what makes an interrupted batch cheap — the most that can be lost is the one
    /// call in flight.
    /// </summary>
    public int MaximumItemsPerTick { get; init; } = 4;

    /// <summary>
    /// Batches advanced per organization per tick. One at a time keeps a customer who queued three
    /// stages from holding the sweep against every other customer.
    /// </summary>
    public int MaximumJobsPerTick { get; init; } = 1;

    /// <summary>
    /// Hard ceiling on the exercises one batch may cover. «Все упражнения этапа» is a sentence, not a
    /// budget: a stage with four hundred exercises is four hundred LLM calls that nobody will ever
    /// review one by one. Above this the start request is refused with the count, so the РОП narrows
    /// the stage rather than discovering the bill afterwards.
    /// </summary>
    public int MaximumItemsPerJob { get; init; } = 60;
}
