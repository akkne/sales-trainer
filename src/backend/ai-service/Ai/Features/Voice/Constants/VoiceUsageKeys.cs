namespace Sellevate.Ai.Features.Voice.Constants;

/// <summary>
/// The Redis key vocabulary of the voice reservation gate.
///
/// <para>
/// Every part of a gate key is load-bearing at run time rather than at compile time: the counter a
/// reservation increments and the counter its refund decrements are found by rebuilding the same
/// string minutes apart, in a different method, from a clock that may have crossed midnight. A
/// window name spelled differently in the two places does not fail — it silently creates a second
/// counter, so the reservation is never returned and the organization loses the minutes.
/// </para>
///
/// <para>
/// The organization prefix is what makes "no ai-service key is shared across organizations"
/// checkable by reading key names (Phase 40.11), so it is applied by the one key builder and never
/// assembled ad hoc.
/// </para>
/// </summary>
public static class VoiceUsageKeys
{
    /// <summary>Key segment naming the per-organization namespace every ai-service key sits in.</summary>
    public const string OrganizationPrefix = "org";

    /// <summary>Key segment naming the voice-usage family.</summary>
    public const string VoiceUsagePrefix = "voice";

    /// <summary>Window resetting at the next UTC midnight.</summary>
    public const string DayWindow = "day";

    /// <summary>Window resetting at the start of the next UTC month.</summary>
    public const string MonthWindow = "month";

    /// <summary>Key segment separator.</summary>
    public const string Separator = ":";
}
