namespace Sellevate.Learning.Features.SkillTree.Models;

/// <summary>
/// The learner's own headline numbers, as the profile screen shows them.
///
/// <para>
/// These live here rather than on identity-service's <c>/profile</c> because learning-service owns
/// the rows they are computed from. Identity used to answer them and returned hard-coded zeros after
/// the microservices split, which is why the profile screen reported 0% accuracy to people whose
/// lessons averaged 94%.
/// </para>
///
/// <para>
/// <see cref="AverageExerciseScore"/> is the mean best score over completed lessons — the same
/// definition the skill tree uses for its per-skill accuracy, so the two screens agree. It is
/// <see langword="null"/>, not 0, when nothing has been completed yet: "no data" and "scored zero"
/// are different answers and the UI must be able to tell them apart.
/// </para>
/// </summary>
public sealed record LearningProgressSummaryDto(
    int CompletedSkillCount,
    int TotalSkillCount,
    int CompletedLessonCount,
    int? AverageExerciseScore
);
