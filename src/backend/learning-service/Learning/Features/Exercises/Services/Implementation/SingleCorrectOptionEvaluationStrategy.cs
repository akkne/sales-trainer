using System.Text.Json;
using Sellevate.Learning.Features.Exercises.Constants;
using Sellevate.Learning.Features.Exercises.Models;
using Sellevate.Learning.Features.Exercises.Services.Abstract;

namespace Sellevate.Learning.Features.Exercises.Services.Implementation;

/// <summary>
/// Grading shared by every exercise type that shows a list of options of which exactly one is
/// correct, and asks the learner to pick an index.
///
/// <para>
/// <b>An out-of-range index is a wrong answer, not an error.</b> A stale client that sends an index
/// past the end of a shortened option list scores zero rather than throwing: the learner's submission
/// is recorded either way, and failing it would lose the attempt. A missing <c>selectedOptionIndex</c>
/// is different — nothing was answered, so it is refused.
/// </para>
///
/// <para>
/// <b>Chosen over duplicating the method per type.</b> <c>choose_option</c> and <c>fill_blank</c>
/// differ only in what the author writes around the options — a situation versus a sentence with a gap
/// — and not at all in how an answer is judged. Two copies of this drifted apart is how one type ends
/// up rewarding an index the other rejects. Each concrete type stays its own class so it keeps its own
/// key in <see cref="ExerciseEvaluationFactory"/> and its own DI registration.
/// </para>
/// </summary>
internal abstract class SingleCorrectOptionEvaluationStrategy : IExerciseEvaluationStrategy
{
    public abstract string SupportedExerciseType { get; }

    public Task<ExerciseEvaluationResult> EvaluateAnswerAsync(
        JsonElement exerciseContent,
        JsonElement userAnswer,
        CancellationToken cancellationToken = default)
    {
        if (!userAnswer.TryGetProperty(ExerciseAnswerFields.SelectedOptionIndex, out var selectedIndexElement)
            || !selectedIndexElement.TryGetInt32(out var selectedIndex))
            throw new ExerciseAnswerValidationException(
                $"Answer must contain integer field '{ExerciseAnswerFields.SelectedOptionIndex}'.");

        if (!exerciseContent.TryGetProperty(ExerciseContentFields.Options, out var optionsElement))
            throw new ExerciseAnswerValidationException(
                $"Exercise content is missing '{ExerciseContentFields.Options}'.");

        var options = optionsElement.EnumerateArray().ToList();

        var isCorrect = false;
        if (selectedIndex >= 0 && selectedIndex < options.Count)
        {
            isCorrect = options[selectedIndex].GetProperty(ExerciseContentFields.IsCorrect).GetBoolean();
        }

        // docs/AUDIT_PROD.md X-6: the learner content strips `is_correct`, so the client cannot show
        // which option was right on a wrong answer — hand back the correct index now that grading has
        // already needed it.
        var correctOptionIndex = options.FindIndex(option =>
            option.TryGetProperty(ExerciseContentFields.IsCorrect, out var isCorrectElement)
            && isCorrectElement.ValueKind == JsonValueKind.True);

        string? explanation = null;
        if (exerciseContent.TryGetProperty(ExerciseContentFields.Explanation, out var explanationElement))
            explanation = explanationElement.GetString();

        return Task.FromResult(new ExerciseEvaluationResult(
            IsCorrect: isCorrect,
            Score: isCorrect ? ExerciseScores.Maximum : ExerciseScores.Minimum,
            Explanation: explanation,
            AiFeedback: null,
            CorrectAnswer: correctOptionIndex >= 0
                ? new ExerciseCorrectAnswerDto(CorrectOptionIndex: correctOptionIndex)
                : null));
    }
}
