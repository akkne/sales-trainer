namespace Sellevate.Learning.Features.Exercises.Models;

/// <summary>
/// What the learner is allowed to know about the correct answer, revealed only inside a
/// <see cref="ExerciseSubmissionResultDto"/> — i.e. only after they have already answered — and never
/// inside the pre-submission exercise body (<c>ExerciseService.StripAnswerKeyFields</c> keeps that one
/// key-free). This is the one contract addition behind docs/AUDIT_PROD.md X-3/X-6/X-8: those three bugs
/// all trace back to a component reading an answer-key field (<c>correct_position</c>, <c>is_correct</c>,
/// <c>is_mistake</c>) that the learner API strips from the exercise content, so the field always reads
/// as absent. Grading the answer already requires knowing the correct one; this DTO is that knowledge
/// handed back on the one response that is allowed to carry it.
///
/// <para>
/// Each exercise type populates only the field it needs — <c>null</c> everywhere else — because a
/// second field would be meaningless for that type's grading. Free-form/AI-graded types
/// (<c>free_text</c>, <c>rewrite</c>, <c>ai_dialogue</c>, <c>evaluate_call</c>) have nothing to reveal in
/// this shape at all, so their evaluation result leaves this whole object <c>null</c>.
/// </para>
/// </summary>
public record ExerciseCorrectAnswerDto(
    /// <summary>
    /// <c>choose_option</c> / <c>fill_blank</c>: index into the exercise's <c>options</c> array of the
    /// one that is correct.
    /// </summary>
    int? CorrectOptionIndex = null,

    /// <summary>
    /// <c>reorder</c>: the exercise's <c>items</c> indices in their correct order — the same shape the
    /// learner submits as <c>{order: number[]}</c>, so the client can diff its own submission against
    /// this to mark each row.
    /// </summary>
    IReadOnlyList<int>? Order = null,

    /// <summary>
    /// <c>spot_mistake</c>: index into the exercise's <c>dialogue</c> array of the line that is the
    /// planted mistake.
    /// </summary>
    int? CorrectLineIndex = null
);
