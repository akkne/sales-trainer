namespace Sellevate.Ai.Features.ContentAdaptation.Models;

/// <summary>
/// Phase 40.32. The closed vocabulary the reviewer may use to say what is wrong with an exercise.
///
/// <para>
/// <b>Codes, not sentences</b> — the arrangement <c>MaterialGapCodes</c> established for refusals,
/// applied to critique for the same three reasons: a model that writes the complaint writes a
/// different one every run, it cannot be translated, and a free-text critique cannot be counted, so
/// nobody could ever answer «стало ли лучше» about a customer's library. The Russian sentences and
/// the blocking/advisory split live in
/// <c>Sellevate.Learning.Common.Constants.ContentReviewFindingCodes</c>; a code this list does not
/// contain is dropped there rather than reaching a customer as an empty bullet.
/// </para>
///
/// <para>
/// Redeclared here rather than shared, for the reason <c>MaterialGapCodes</c> gives: this is the wire
/// shape of an internal endpoint and it must not move when a learning-service constant does.
/// </para>
/// </summary>
public static class ContentReviewCodes
{
    /// <summary>More than one answer is defensible.</summary>
    public const string AmbiguousCorrectAnswer = "ambiguous_correct_answer";

    /// <summary>Two options are correct as written, not merely arguable.</summary>
    public const string MultipleCorrectAnswers = "multiple_correct_answers";

    /// <summary>Nobody would ever pick the wrong options.</summary>
    public const string ObviousDistractors = "obvious_distractors";

    /// <summary>The wording of the task contains its own answer.</summary>
    public const string AnswerGivenAway = "answer_given_away";

    /// <summary>Free-answer criteria that cannot be checked — «ответил хорошо».</summary>
    public const string UnmeasurableCriteria = "unmeasurable_criteria";

    /// <summary>The exercise says which answer is right and never says why.</summary>
    public const string MissingExplanation = "missing_explanation";

    /// <summary>The correct answer contains or rewards one of the organization's banned claims.</summary>
    public const string BannedClaimRewarded = "banned_claim_rewarded";

    public static readonly string[] All =
    [
        AmbiguousCorrectAnswer,
        MultipleCorrectAnswers,
        ObviousDistractors,
        AnswerGivenAway,
        UnmeasurableCriteria,
        MissingExplanation,
        BannedClaimRewarded
    ];

    public static bool IsKnown(string? code) => code is not null && All.Contains(code);
}
