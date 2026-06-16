using SalesTrainer.Api.Features.Exercises.Services.Abstract;
using SalesTrainer.Api.Features.Exercises.Services.Implementation;

namespace SalesTrainer.Api.Features.Exercises;

public static class ExerciseServiceCollectionExtensions
{
    public static IServiceCollection AddExerciseFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IExerciseService, ExerciseService>();
        services.AddScoped<ExerciseEvaluationFactory>();

        // Non-AI exercise types (10 total)
        services.AddScoped<IExerciseEvaluationStrategy, ChooseOptionEvaluationStrategy>();  // choose_option
        services.AddScoped<IExerciseEvaluationStrategy, FillBlankEvaluationStrategy>();     // fill_blank
        services.AddScoped<IExerciseEvaluationStrategy, ReorderEvaluationStrategy>();       // reorder
        services.AddScoped<IExerciseEvaluationStrategy, MatchPairsEvaluationStrategy>();    // match_pairs
        services.AddScoped<IExerciseEvaluationStrategy, CategorizeEvaluationStrategy>();    // categorize

        // AI-powered exercise types
        services.AddScoped<IExerciseEvaluationStrategy, SpotMistakeEvaluationStrategy>();   // spot_mistake
        services.AddScoped<IExerciseEvaluationStrategy, RewriteEvaluationStrategy>();       // rewrite
        services.AddScoped<IExerciseEvaluationStrategy, AiDialogueEvaluationStrategy>();    // ai_dialogue
        services.AddScoped<IExerciseEvaluationStrategy, EvaluateCallEvaluationStrategy>();  // evaluate_call
        services.AddScoped<IExerciseEvaluationStrategy, FreeTextEvaluationStrategy>();      // free_text

        // Non-graded theory cards (no AI; reaching the end completes the lesson)
        services.AddScoped<IExerciseEvaluationStrategy, TheoryCardEvaluationStrategy>();    // theory_card

        return services;
    }
}
