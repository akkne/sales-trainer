namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// Phase 40.25. What has happened to one review note (docs/TENANCY/ASSIGNMENTS.md §4.1).
///
/// <para>
/// <b>One vocabulary, but not every word belongs to every kind.</b> A coaching note ends when the
/// manager has read it; a dispute ends in a verdict, and the verdict has two possible values because
/// "the РОП looked at it" and "the РОП agreed with you" are the difference between a process and a
/// rubber stamp. <see cref="IsTerminalFor"/> is what keeps a note from being "upheld" and a dispute
/// from being closed by the person who filed it, and a database check constraint says the same thing
/// so a future writer cannot forget.
/// </para>
/// </summary>
public static class DialogReviewStatuses
{
    /// <summary>Written, delivered, waiting on the other party. The only status a row is created in.</summary>
    public const string Open = "open";

    /// <summary>Coaching note only: the manager has read it. Not a verdict — there is nothing to judge.</summary>
    public const string Acknowledged = "acknowledged";

    /// <summary>
    /// Dispute only: the РОП agrees the grade was wrong. This is the row a prompt-tuning dataset
    /// wants — a human-labelled disagreement with a specific machine grade.
    /// </summary>
    public const string Upheld = "upheld";

    /// <summary>Dispute only: the РОП looked and the grade stands. Just as much labelled data as an upheld one.</summary>
    public const string Rejected = "rejected";

    public static bool IsKnown(string status) => status is Open or Acknowledged or Upheld or Rejected;

    /// <summary>Whether <paramref name="status"/> is a legal ending for a row of <paramref name="kind"/>.</summary>
    public static bool IsTerminalFor(string kind, string status) => kind switch
    {
        DialogReviewKinds.CoachingNote => status == Acknowledged,
        DialogReviewKinds.ScoreDispute => status is Upheld or Rejected,
        _ => false,
    };
}
