using System.Text.Json;
using Sellevate.Learning.Features.Exercises.Models;

namespace Sellevate.Learning.Features.Exercises.Services.Abstract;

/// <summary>
/// Judges a submitted answer for exactly one exercise type.
///
/// <para>
/// <b>Implementations must be side-effect free.</b> Grading is called before the write transaction
/// opens and may be retried; nothing here may record an attempt, touch progress, or publish an event.
/// A malformed answer is signalled with <c>ExerciseAnswerValidationException</c>, which the controller
/// maps to a 400 — an answer that is merely wrong is a result, not an exception.
/// </para>
///
/// <para>
/// The content passed in has already had organization placeholders rendered, so a strategy compares
/// against the same words the learner saw.
/// </para>
/// </summary>
public interface IExerciseEvaluationStrategy
{
    /// <summary>
    /// The <c>ExerciseTypes</c> key this strategy answers for. Must be unique across every registered
    /// strategy: <see cref="Sellevate.Learning.Features.Exercises.Services.Implementation.ExerciseEvaluationFactory"/>
    /// keys its lookup by this value and a duplicate would throw at startup.
    /// </summary>
    string SupportedExerciseType { get; }

    Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default);
}
