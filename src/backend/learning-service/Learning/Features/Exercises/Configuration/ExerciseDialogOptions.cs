namespace Sellevate.Learning.Features.Exercises.Configuration;

/// <summary>
/// Tuning for the interactive <c>ai_dialogue</c> exercise: how long a half-finished practice
/// conversation survives, and how many turns it runs for when the author did not say.
///
/// <para>
/// <b>None of these change what a dialogue means, only how long it lasts.</b> They are here rather
/// than as constants so a support engineer can widen the window for a customer without a rebuild, and
/// none of them is a secret.
/// </para>
/// </summary>
public sealed class ExerciseDialogOptions
{
    public const string SectionName = "ExerciseDialog";

    /// <summary>
    /// How long a dialogue's transcript is kept in Redis. Long enough that a learner can be
    /// interrupted and come back within the working day; short enough that abandoned transcripts —
    /// real tenant data — expire on their own rather than accumulating.
    /// </summary>
    public int ChatStateTtlHours { get; init; } = 24;

    /// <summary>
    /// Turn limit for an exercise whose content omits <c>max_turns</c>. A limit always applies: an
    /// unbounded practice dialogue is an unbounded bill.
    /// </summary>
    public int DefaultMaximumTurns { get; init; } = 10;

    /// <summary>
    /// Only used when no AI provider is configured at all, in the canned-reply fallback: how many of
    /// the learner's turns must have passed before a thank-you is read as the conversation ending.
    /// Has no effect on a normally configured deployment.
    /// </summary>
    public int FallbackCompletionTurnThreshold { get; init; } = 3;
}
