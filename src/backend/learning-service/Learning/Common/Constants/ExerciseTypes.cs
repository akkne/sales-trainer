namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// The exercise type keys, split by how an answer to each is judged.
///
/// <para>
/// <b>Persisted and compared in SQL.</b> Every <c>Exercise.Type</c> column, every frozen
/// <c>LessonVersion</c> snapshot, and the seeder's import files hold these exact strings, and the
/// frontend editors mirror them. Extend this class; never change a value — a renamed key silently
/// orphans every existing row of that type.
/// </para>
///
/// <para>
/// <b><see cref="AiPowered"/> is a partition, not a hint.</b> <c>ExerciseEvaluationFactory</c> builds an
/// AI strategy for exactly these types and expects a DI-registered deterministic strategy for the rest,
/// so moving a type between the two groups is what changes how it is graded. A type added to
/// <see cref="All"/> and to neither group has no strategy at all and throws when a learner answers it.
/// </para>
/// </summary>
public static class ExerciseTypes
{
    public const string ChooseOption = "choose_option";
    public const string FillBlank = "fill_blank";
    public const string Reorder = "reorder";
    public const string MatchPairs = "match_pairs";
    public const string Categorize = "categorize";
    public const string SpotMistake = "spot_mistake";
    public const string Rewrite = "rewrite";
    public const string AiDialogue = "ai_dialogue";
    public const string EvaluateCall = "evaluate_call";
    public const string FreeText = "free_text";
    public const string TheoryCard = "theory_card";

    public static readonly string[] All =
    [
        ChooseOption,
        FillBlank,
        Reorder,
        MatchPairs,
        Categorize,
        SpotMistake,
        Rewrite,
        AiDialogue,
        EvaluateCall,
        FreeText,
        TheoryCard
    ];

    public static readonly string[] AiPowered =
    [
        SpotMistake,
        Rewrite,
        AiDialogue,
        EvaluateCall,
        FreeText
    ];
}
