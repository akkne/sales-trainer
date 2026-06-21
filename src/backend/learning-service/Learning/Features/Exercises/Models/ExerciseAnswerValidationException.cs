namespace Sellevate.Learning.Features.Exercises.Models;

/// <summary>
/// Thrown when a submitted exercise answer is structurally malformed
/// (missing required fields or wrong field types). Maps to HTTP 400.
/// </summary>
public sealed class ExerciseAnswerValidationException : Exception
{
    public ExerciseAnswerValidationException(string message) : base(message) { }
}
