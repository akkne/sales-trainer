using System.Text.Json;
using SalesTrainer.Api.Features.Exercises.Models;
using SalesTrainer.Api.Features.Exercises.Services.Abstract;

namespace SalesTrainer.Api.Features.Exercises.Services.Implementation;

/// <summary>
/// "Evaluates" a theory_card. Theory cards carry no answer and are never graded —
/// reaching the end of a theory lesson is what counts as completion. Submitting one
/// (done once, for the last card, when the learner finishes the story) always
/// returns success so the lesson is marked complete and the fixed theory XP is awarded.
/// No AI is ever invoked.
/// </summary>
internal sealed class TheoryCardEvaluationStrategy : IExerciseEvaluationStrategy
{
    public string SupportedExerciseType => ExerciseTypes.TheoryCard;

    public Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ExerciseEvaluationResult(
            IsCorrect: true,
            Score: 100,
            Explanation: null,
            AiFeedback: null));
    }
}
