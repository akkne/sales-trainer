namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// Phase 40.26. Who a "remind" press reaches (docs/TENANCY/ASSIGNMENTS.md §5).
///
/// <para>
/// <b>This vocabulary exists because the digest names a narrower set than the button used to
/// nudge.</b> 40.23's reminder went to everybody unfinished, which was right when the only way to
/// press it was to be looking at the assignment. 40.26 sends a notice listing the people who have
/// not started and puts the button in it — and a button that then messages twelve people when the
/// notice named five is the product doing something other than what it just said.
/// </para>
///
/// <para>
/// Two values and no more. Reminding the people under the threshold as a group was considered and
/// left out: somebody who tried four times needs coaching, and «вы ещё не завершили» is the product
/// telling them something they know better than it does.
/// </para>
/// </summary>
public static class AssignmentReminderScopes
{
    /// <summary>
    /// Everybody who has not completed it — not started, in progress, and under the threshold.
    /// The default, so the route behaves exactly as it did in 40.23 when nothing is asked for.
    /// </summary>
    public const string Unfinished = "unfinished";

    /// <summary>Only the people who have never opened it — the list the 40.26 digest spells out.</summary>
    public const string NotStarted = "not_started";

    public static bool IsKnown(string scope)
        => scope is Unfinished or NotStarted;
}
