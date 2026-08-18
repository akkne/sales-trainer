using Sellevate.Learning.Common.Constants;

namespace Sellevate.Learning.Features.Exercises.Services.Implementation;

/// <summary>
/// Grades <c>choose_option</c>: a selling situation with several possible replies, one of which is
/// right. Judged entirely by <see cref="SingleCorrectOptionEvaluationStrategy"/>.
/// </summary>
internal sealed class ChooseOptionEvaluationStrategy : SingleCorrectOptionEvaluationStrategy
{
    public override string SupportedExerciseType => ExerciseTypes.ChooseOption;
}
