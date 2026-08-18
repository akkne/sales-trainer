namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.24. One assignment's repeat schedule, parsed out of the <c>repeat_schedule</c> jsonb
/// column (docs/TENANCY/ASSIGNMENTS.md §2.1).
///
/// <para>
/// The whole vocabulary reduces to this: an ordered list of whole-day offsets, each one a wave. The
/// wave's <b>ordinal position</b> in that list — 1-based — is what identifies it forever after, not
/// the day number, because the list stays editable while the assignment is active. A РОП who moves
/// the second wave from +21 to +28 has moved a wave; a РОП who moves the first from +7 to +5 has not
/// asked for the wave that already went out to go out again.
/// </para>
/// </summary>
/// <param name="OffsetDays">
/// Strictly ascending, at least one, at most <see cref="AssignmentRepeatScheduleLimits.MaximumWaveCount"/>.
/// </param>
internal sealed record AssignmentRepeatSchedule(IReadOnlyList<int> OffsetDays);

/// <summary>Phase 40.24. The bounds the schedule vocabulary is validated against.</summary>
internal static class AssignmentRepeatScheduleLimits
{
    /// <summary>
    /// Four waves. The roadmap asks for two, and a bound exists so that a hand-written body cannot
    /// turn one training into a year of homework — every wave is a fan-out that writes a progress row
    /// and a notification per person.
    /// </summary>
    public const int MaximumWaveCount = 4;

    /// <summary>
    /// Half a year. Past that the assignment is no longer a repeat of a training anybody remembers
    /// attending, and the origin's cohort has usually turned over.
    /// </summary>
    public const int MaximumOffsetDays = 180;

    /// <summary>The roadmap's schedule, used when <c>offsetDays</c> is omitted.</summary>
    public static readonly IReadOnlyList<int> DefaultOffsetDays = [7, 21];
}
