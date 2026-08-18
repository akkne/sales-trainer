namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// Phase 40.32. What can be wrong with an exercise a human wrote, as a closed vocabulary of seven
/// codes — the roadmap's three named defects plus the four the same reading of the content turns up
/// for free.
///
/// <para>
/// <b>A paragraph of advice is not a review.</b> This is 40.28's rule applied to a different
/// question: ai-service returns codes and nothing else (<c>ContentReviewCodes</c>, the same list on
/// the other side of the wire), and the sentences the РОП reads live here. A model asked to phrase
/// the complaint phrases it differently every time, occasionally demands something the product
/// cannot do, and cannot be translated; worse, a free-text critique cannot be counted, so nobody
/// could ever answer «стало ли лучше» about a customer's library.
/// </para>
///
/// <para>
/// <b>Why this exists at all.</b> The customer's own РОП writes exercises in our editor, and the
/// exercises they write are, to their team, indistinguishable from ours. A question with two
/// defensible answers teaches the salesperson that the product is arbitrary; a free-answer exercise
/// whose criteria are «ответил хорошо» produces grading nobody can appeal. Neither is Sellevate's
/// mistake and both become Sellevate's perceived quality (roadmap 40.32). Reviewing every customer's
/// content by hand does not scale past the third customer, which is why the reviewer is a machine
/// and why it only ever <b>reports</b>.
/// </para>
/// </summary>
public static class ContentReviewFindingCodes
{
    /// <summary>Roadmap: «правильный ответ неоднозначен». More than one option is defensible.</summary>
    public const string AmbiguousCorrectAnswer = "ambiguous_correct_answer";

    /// <summary>
    /// Two options are correct as written, not merely arguable. Split from the one above because the
    /// fix differs: an ambiguity is rewritten, a duplicate is deleted.
    /// </summary>
    public const string MultipleCorrectAnswers = "multiple_correct_answers";

    /// <summary>Roadmap: «дистракторы слишком очевидны». Nobody would ever pick the wrong options.</summary>
    public const string ObviousDistractors = "obvious_distractors";

    /// <summary>The wording of the question contains its own answer, so the exercise measures reading.</summary>
    public const string AnswerGivenAway = "answer_given_away";

    /// <summary>
    /// Roadmap: «критерии свободного ответа неизмеримы». «Ответил хорошо» cannot be checked by a
    /// grader, human or otherwise, and produces a score nobody can defend to the person who got it.
    /// </summary>
    public const string UnmeasurableCriteria = "unmeasurable_criteria";

    /// <summary>The exercise says which answer is right and never says why. A drill, not a lesson.</summary>
    public const string MissingExplanation = "missing_explanation";

    /// <summary>
    /// The correct answer contains, or rewards, one of the organization's own banned claims — the
    /// sharpest finding in the list and the reason the review is given the profile at all. An
    /// exercise like this does not merely permit a forbidden promise, it teaches it and then scores
    /// the salesperson well for making it (docs/CONTENT_PARAMETERIZATION.md, 40.19).
    /// </summary>
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

    /// <summary>
    /// A finding that makes the exercise actively harmful rather than merely weak. Two of the seven,
    /// and the distinction is what stops a queue of sixty advisory notes burying the one that says
    /// «это упражнение учит обещать доходность».
    /// </summary>
    public const string BlockingSeverity = "blocking";

    public const string AdvisorySeverity = "advisory";

    private static readonly Dictionary<string, string> Severities = new(StringComparer.Ordinal)
    {
        [AmbiguousCorrectAnswer] = BlockingSeverity,
        [MultipleCorrectAnswers] = BlockingSeverity,
        [ObviousDistractors] = AdvisorySeverity,
        [AnswerGivenAway] = AdvisorySeverity,
        [UnmeasurableCriteria] = BlockingSeverity,
        [MissingExplanation] = AdvisorySeverity,
        [BannedClaimRewarded] = BlockingSeverity
    };

    /// <summary>
    /// The sentence the РОП reads: Russian, naming the defect and the edit that fixes it, never
    /// «улучшите формулировку». The wording is fixed so that support can recognise a report and so
    /// that two runs over the same exercise produce the same complaint.
    /// </summary>
    private static readonly Dictionary<string, string> Messages = new(StringComparer.Ordinal)
    {
        [AmbiguousCorrectAnswer] =
            "Правильный ответ неоднозначен: как минимум ещё один вариант можно защитить. Уточните "
            + "формулировку ситуации или сделайте разницу между вариантами явной.",
        [MultipleCorrectAnswers] =
            "Верных вариантов больше одного. Оставьте один правильный, а остальные переформулируйте "
            + "так, чтобы они были неверными по существу, а не по мелочи.",
        [ObviousDistractors] =
            "Неверные варианты слишком очевидны — их не выберет никто. Замените их на то, что "
            + "менеджеры действительно говорят в этой ситуации.",
        [AnswerGivenAway] =
            "Ответ подсказан самой формулировкой задания. Уберите из вопроса слова, которые "
            + "указывают на верный вариант.",
        [UnmeasurableCriteria] =
            "Критерии оценки свободного ответа нельзя проверить («ответил хорошо», «был вежлив»). "
            + "Замените их на наблюдаемые факты: назвал цифру, задал уточняющий вопрос, "
            + "предложил следующий шаг.",
        [MissingExplanation] =
            "Не объяснено, почему верный ответ верен. Добавьте пояснение — без него упражнение "
            + "проверяет, но не учит.",
        [BannedClaimRewarded] =
            "Верный ответ содержит или поощряет обещание из вашего списка запрещённых. Такое "
            + "упражнение учит менеджера его произносить и ставит за это высокий балл — "
            + "переформулируйте ответ."
    };

    public static bool IsKnown(string? code) => code is not null && Messages.ContainsKey(code);

    /// <summary>
    /// The sentence for a code, or <see langword="null"/> for a code this service does not know. An
    /// unknown code is dropped rather than shown blank — the vocabulary is closed on purpose, and a
    /// code a model invented would otherwise reach a customer as an empty bullet.
    /// </summary>
    public static string? MessageFor(string? code)
        => code is not null && Messages.TryGetValue(code, out var message) ? message : null;

    public static string SeverityFor(string? code)
        => code is not null && Severities.TryGetValue(code, out var severity) ? severity : AdvisorySeverity;
}
