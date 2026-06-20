namespace Sellevate.Learning.Common.Constants;

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
