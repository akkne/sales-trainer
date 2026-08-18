namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// Phase 40.32. The reviewer's verdict on one exercise: a list of codes, possibly empty.
///
/// <para>
/// Empty is the expected answer, and an empty verdict resolves the item as <c>unchanged</c> — nothing
/// reaches the queue. A reviewer that always finds something is one nobody believes by the tenth
/// exercise.
/// </para>
/// </summary>
public sealed record AiExerciseReview(IReadOnlyList<AiExerciseReviewFinding> Findings);
