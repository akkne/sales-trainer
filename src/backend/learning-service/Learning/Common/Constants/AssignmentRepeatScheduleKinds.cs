namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// Phase 40.24. What an assignment's <c>repeat_schedule</c> may say
/// (docs/TENANCY/ASSIGNMENTS.md §2.1).
///
/// <para>
/// <b>One kind, and the roadmap wrote it.</b> "A shortened version at +7 and +21 days, configured
/// once, then automatic" is a list of day offsets measured from one anchor and nothing else. 40.21
/// shipped the column as "an object naming its kind" so this block could pick the vocabulary without
/// inheriting a guess; the guess this block declines to make is a cron expression. A schedule that
/// can say "every second Tuesday" is a schedule somebody has to be taught, and the thing being
/// scheduled — the decay curve of one training session — has no weekly rhythm to align to.
/// </para>
///
/// <para>
/// <b>An unknown kind is refused on write and unreadable on read</b>, the same asymmetry
/// <see cref="AssignmentCompletionRuleKinds"/> states. Refusing it while an administrator is
/// looking at the screen is the only moment the mistake is cheap: a schedule nobody can parse
/// produces an assignment that silently never repeats, and "the repeats never came" is
/// indistinguishable from "the repeats were never configured" on every screen this product has.
/// </para>
/// </summary>
public static class AssignmentRepeatScheduleKinds
{
    /// <summary>
    /// <c>{"kind":"fixed_offsets","offsetDays":[7,21]}</c> — a shortened re-issue this many days
    /// after the **origin assignment was issued**, once per offset. <c>offsetDays</c> may be omitted,
    /// and then means exactly the roadmap's <c>[7, 21]</c>.
    ///
    /// <para>
    /// Offsets are measured from one fixed anchor rather than chained from the previous wave, so a
    /// wave that fires late — a service restarted, an organization skipped for a tick — cannot push
    /// every later wave later with it.
    /// </para>
    /// </summary>
    public const string FixedOffsets = "fixed_offsets";

    public static bool IsKnown(string kind)
        => kind is FixedOffsets;
}
