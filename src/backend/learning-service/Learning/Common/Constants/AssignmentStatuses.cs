namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// Phase 40.21. The lifecycle of an <c>Assignment</c> row, stored as text so the check constraint
/// and the freeze trigger in migration <c>AddAssignments</c> can read it directly
/// (docs/TENANCY/ASSIGNMENTS.md §1).
///
/// <para>
/// Three values and no more, matching the design document exactly. A fourth — <c>scheduled</c>, for
/// an assignment whose <c>OpensAt</c> is still in the future — was deliberately left out: whether an
/// active assignment is visible yet is a question about <c>OpensAt</c>, and a second place to store
/// the same fact is a second place for it to be wrong.
/// </para>
/// </summary>
public static class AssignmentStatuses
{
    /// <summary>
    /// The one fully mutable state. Nobody has been told about the assignment yet, so everything on
    /// it may still change and the row may still be deleted.
    /// </summary>
    public const string Draft = "draft";

    /// <summary>
    /// Issued. From here on the database refuses any change to what the assignment asks for —
    /// <c>Content</c>, <c>CompletionRule</c>, <c>SourceType</c>, <c>SourceRef</c> — because those are
    /// what every recorded attempt was scored against. Who it is for, when it is due and what it is
    /// called stay editable: adding three people to a running assignment and extending a deadline are
    /// ordinary acts, not corruption.
    /// </summary>
    public const string Active = "active";

    /// <summary>
    /// Finished, by the deadline passing or by the РОП saying so. Kept forever — the progress rows
    /// pointing at it are the record of who did what, and 40.25's dashboard reads exactly them.
    /// </summary>
    public const string Closed = "closed";

    public static bool IsKnown(string status)
        => status is Draft or Active or Closed;
}
