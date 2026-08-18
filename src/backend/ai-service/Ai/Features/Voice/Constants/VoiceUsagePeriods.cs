namespace Sellevate.Ai.Features.Voice.Constants;

/// <summary>
/// Names of the windows a voice reservation can be refused by. These travel to the browser as the
/// <c>period</c> field of the 429 body, which the client renders as "you have used your day" versus
/// "your month", so they are a wire vocabulary and not log text.
/// </summary>
public static class VoiceUsagePeriods
{
    /// <summary>The caller's own day allowance.</summary>
    public const string Daily = "daily";

    /// <summary>The caller's own month allowance.</summary>
    public const string Monthly = "monthly";

    /// <summary>
    /// Prefix applied to an organization-level window, so a 429 raised by the shared quota reads
    /// «organization daily» and is distinguishable from the caller's own limit on the same screen.
    /// </summary>
    public const string OrganizationPrefix = "organization";
}
