namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// Phase 40.22. What an assignment may count as done (docs/TENANCY/ASSIGNMENTS.md §1.1).
///
/// <para>
/// <b>Every kind here is a quality bar, and that is the entire point of the vocabulary.</b> 40.21
/// deliberately shipped <c>completion_rule</c> as "an object naming its kind" and nothing more, so
/// that this block could decide what the kinds mean without inheriting a guess. The one shape that
/// is <i>not</i> in the vocabulary — and never will be — is "opened everything": a rule that
/// completes on a click means a team clicks through in four minutes, the dashboard reads 100%, and
/// the number is a lie the РОП eventually catches. That is the difference between a training tool
/// and a compliance-theatre tool, and a vocabulary is the cheapest place to make it unreachable.
/// </para>
///
/// <para>
/// <b>An unknown kind is refused, not ignored.</b> Tolerating one would mean an assignment that
/// nobody can complete and nobody can see is broken — the same silent failure as no threshold at
/// all, wearing a discriminator. The refusal happens at create/update time, where an administrator
/// is present to read the message.
/// </para>
///
/// <para>
/// <b>Room for later blocks.</b> The rule stays a discriminated JSON object, so 40.24 and 40.25 can
/// add a kind without a migration and without touching a stored rule: an issued assignment's rule
/// is frozen by the 40.21 trigger, so existing rows keep meaning what they meant. What a new kind
/// owes is a reading of the two numbers <c>AssignmentProgressRecords</c> carries — what one attempt
/// is, and what the 0–100 score of an attempt is — because those are what the РОП's screen shows.
/// </para>
/// </summary>
public static class AssignmentCompletionRuleKinds
{
    /// <summary>
    /// <c>{"kind":"dialog_score","minimumScore":70,"requiredCount":3}</c> — the roadmap's first
    /// example, "3 диалога с оценкой ≥70". One attempt is one evaluated practice conversation on one
    /// of the assignment's <c>dialog_scenario</c> items; the rule is met once
    /// <c>requiredCount</c> distinct conversations have each scored at least <c>minimumScore</c>.
    ///
    /// <para>
    /// Counting conversations rather than averaging them is deliberate: an average lets one strong
    /// call carry two weak ones, and the skill being trained is doing it right repeatedly.
    /// </para>
    /// </summary>
    public const string DialogScore = "dialog_score";

    /// <summary>
    /// <c>{"kind":"exercise_accuracy","minimumAccuracyPercent":80}</c> — the roadmap's second
    /// example, "точность по упражнениям ≥80%". One attempt is one exercise submission against the
    /// assignment's pinned lesson version; accuracy is correct submissions over all submissions,
    /// the same definition <c>LessonAccuracyService</c> already reports to the admin panel.
    ///
    /// <para>
    /// Accuracy over submissions rather than over exercises-eventually-answered-correctly is what
    /// makes the bar unfakeable: brute-forcing a set until everything is green lowers accuracy
    /// instead of raising it.
    /// </para>
    /// </summary>
    public const string ExerciseAccuracy = "exercise_accuracy";

    public static bool IsKnown(string kind)
        => kind is DialogScore or ExerciseAccuracy;
}
