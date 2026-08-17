namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// Phase 40.21. Where an assignment came from (docs/TENANCY/ASSIGNMENTS.md §1, §3.4).
///
/// <para>
/// The value decides how <c>Assignment.SourceRef</c> reads, which is why the two columns are
/// constrained together rather than separately.
/// </para>
/// </summary>
public static class AssignmentSourceTypes
{
    /// <summary>
    /// Generated from material the organization uploaded — a deck, notes, a recording, pasted text.
    /// <c>SourceRef</c> names that material, or the frozen library content it produced.
    /// </summary>
    public const string Training = "training";

    /// <summary>Written by hand by the РОП. <c>SourceRef</c> is null and the check constraint says so.</summary>
    public const string Manual = "manual";

    /// <summary>
    /// Proposed by the dashboard because the team is failing at something (§3.4). <c>SourceRef</c>
    /// names the metric that triggered it, so the loop from "the number is bad" to "here is practice
    /// for it" stays traceable in both directions.
    /// </summary>
    public const string GapDetected = "gap_detected";

    public static bool IsKnown(string sourceType)
        => sourceType is Training or Manual or GapDetected;
}
