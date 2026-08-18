namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// Phase 40.31. Why a real gap is not being offered as a button (docs/TENANCY/ASSIGNMENTS.md §3.4).
///
/// <para>
/// <b>A suppressed gap is reported, not hidden.</b> A panel that silently shows nothing cannot be
/// distinguished from a panel that is broken, and «почему мне ничего не предлагают» is the question
/// that gets the feature switched off. So the endpoint returns two lists: what to press, and what
/// was deliberately left unpressed and until when.
/// </para>
/// </summary>
public static class TeamSkillGapSuppressionReasons
{
    /// <summary>The РОП said no, and the refusal has not expired or been overtaken by a worse number.</summary>
    public const string Dismissed = "dismissed";

    /// <summary>
    /// A content generation run for this stage is already alive — structuring, waiting at the
    /// checkpoint, generating, or sitting refused for thin material. Offering the button again would
    /// buy a second lesson about the same weakness, and 40.27 spent a block making sure one run is
    /// paid for once.
    /// </summary>
    public const string RunInProgress = "run_in_progress";

    /// <summary>
    /// A run for this stage finished recently. The exercises exist; the number has not had time to
    /// move yet, and re-offering would be the panel asking for the same work twice.
    /// </summary>
    public const string RecentlyAddressed = "recently_addressed";
}
