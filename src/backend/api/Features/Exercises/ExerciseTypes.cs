namespace SalesTrainer.Api.Features.Exercises;

/// <summary>
/// All supported exercise type identifiers.
/// Change values here to rename types across the entire backend.
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

    /// <summary>
    /// Theory card — a non-graded "story" card the learner swipes through. A lesson
    /// made entirely of theory_card exercises is a theory lesson (see NEW_EXERCISE_TYPES.md).
    /// </summary>
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
