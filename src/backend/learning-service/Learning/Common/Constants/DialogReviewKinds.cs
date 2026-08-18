namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// Phase 40.25. The two directions the feedback loop of docs/TENANCY/ASSIGNMENTS.md §4.1 runs in.
///
/// <para>
/// <b>One table with a kind rather than two tables.</b> A coaching note and a disputed score are the
/// same object seen from either end: an annotation on a fragment of one conversation, written by one
/// party, closed by the other. They share a session, a quoted fragment, a comment, an author, a
/// subject and a resolution; splitting them would duplicate all six and leave two places to get the
/// tenant column, the freeze rules and the quoted-fragment copy right. What genuinely differs is who
/// may close the row and with which words, and that is a per-kind status vocabulary rather than a
/// second schema — see <see cref="DialogReviewStatuses"/> and docs/DECISIONS.md (2026-08-18).
/// </para>
/// </summary>
public static class DialogReviewKinds
{
    /// <summary>
    /// The РОП selected three lines out of a conversation and sent them to the manager with a
    /// comment. Author is the РОП, subject is the manager, and the manager closes it by reading it.
    /// </summary>
    public const string CoachingNote = "coaching_note";

    /// <summary>
    /// The manager says the AI graded them wrongly. Author and subject are the same person, and the
    /// РОП closes it with a verdict.
    ///
    /// <para>
    /// This is the mechanism the roadmap argues cannot be skipped: without it AI grading is a black
    /// box, and the first genuinely disputed score costs the product the team's trust in every
    /// number it shows. The rows it leaves behind are also the labelled data the grading prompts get
    /// tuned on — see docs/TENANCY/sql/40.25_dispute_dataset.sql.
    /// </para>
    /// </summary>
    public const string ScoreDispute = "score_dispute";

    public static bool IsKnown(string kind) => kind is CoachingNote or ScoreDispute;
}
