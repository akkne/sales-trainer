namespace Sellevate.Ai.Features.Dialog.Constants;

/// <summary>
/// The out-of-band markers the feedback and reply contracts are built on: they appear in the prompt
/// text that instructs the model and again in the parser that reads its answer back.
///
/// <para>
/// The pairing is the whole point. A marker changed in the prompt but not in the parser does not
/// fail — the parser simply never finds it and silently falls back, so a learner gets a summary
/// that is the first three sentences of the detailed text and a score of zero. Keeping both ends on
/// one constant makes that pair impossible to half-change.
/// </para>
///
/// <para>
/// The seeded mode prompts in <c>Features/Dialog/Seeders</c> hold their own copies on purpose: those
/// strings are compared byte-for-byte against what is already in the database to decide whether an
/// administrator has customized a prompt, so they are historical records rather than live contract.
/// </para>
/// </summary>
public static class DialogFeedbackMarkup
{
    /// <summary>Splits the short summary from the detailed breakdown in a feedback answer.</summary>
    public const string DetailedSectionDelimiter = "[DETAILED]";

    /// <summary>Matches <c>[XP:number]</c> — the experience-point award at the end of the answer.</summary>
    public const string ExperiencePointsTagPattern = @"\[XP:(\d+)\]";

    /// <summary>Matches <c>[SCORE:number]</c> — the 0–10 grade at the end of the answer.</summary>
    public const string ScoreTagPattern = @"\[SCORE:(\d+)\]";

    /// <summary>Matches either tag, for stripping them out of the text shown to the learner.</summary>
    public const string AnyScoringTagPattern = @"\[(?:XP|SCORE):\d+\]";

    /// <summary>
    /// Legacy hang-up marker. Predates the structured <c>endCall</c> field and is still honoured on
    /// the plain-text fallback path, because a model that ignored the JSON contract may well be an
    /// older prompt's reader too.
    /// </summary>
    public const string LegacyDialogEndMarker = "[DIALOG_END]";

    /// <summary>Number of leading sentences used as the summary when the model omitted the delimiter.</summary>
    public const int SummaryFallbackSentenceCount = 3;
}
