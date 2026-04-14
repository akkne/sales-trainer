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
        FreeText
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
