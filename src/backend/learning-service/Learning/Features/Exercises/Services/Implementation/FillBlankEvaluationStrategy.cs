using Sellevate.Learning.Common.Constants;

namespace Sellevate.Learning.Features.Exercises.Services.Implementation;

/// <summary>
/// Grades <c>fill_blank</c>: a sentence with a gap and several candidate fillers, one of which is
/// right. Judged entirely by <see cref="SingleCorrectOptionEvaluationStrategy"/> — the gap's
/// surrounding text is presentation only and plays no part in scoring.
/// </summary>
internal sealed class FillBlankEvaluationStrategy : SingleCorrectOptionEvaluationStrategy
{
    public override string SupportedExerciseType => ExerciseTypes.FillBlank;
}
