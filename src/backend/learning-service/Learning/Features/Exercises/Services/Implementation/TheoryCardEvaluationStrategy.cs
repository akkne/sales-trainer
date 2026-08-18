using System.Text.Json;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Exercises.Constants;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Exercises.Services.Abstract;

namespace Sellevate.Learning.Features.Exercises.Services.Implementation;

/// <summary>
/// "Grades" <c>theory_card</c>, which asks nothing and therefore always passes.
///
/// <para>
/// <b>Full marks are load-bearing, not a placeholder.</b> A story card is read, not answered, and
/// lesson completion counts attempts on every exercise in the lesson — so a card has to produce a
/// passing attempt or a lesson containing one could never be completed. Scoring it zero would also drag
/// down the lesson accuracy series and every assignment threshold measured over it.
/// </para>
///
/// <para>
/// The submitted answer is ignored entirely; a client may send an empty object.
/// </para>
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
            Score: ExerciseScores.Maximum,
            Explanation: null,
            AiFeedback: null));
    }
}
