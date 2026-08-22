namespace Sellevate.Learning.Features.Admin;

/// <summary>
/// Q-8 (<c>docs/NIGHT_AUDIT_QUESTIONS.md</c>). The whole new order of one lesson's exercises, in one
/// request.
///
/// <para>
/// The alternative — and what the admin and org editors did before this endpoint existed — is one
/// <c>PUT /admin/exercises/{id}</c> per row whose position changed. That persists correctly when
/// every call succeeds, but a partial failure leaves the lesson with duplicated or missing
/// positions and no record of what the operator meant, which is what the owner chose to close by
/// building this route. So the request carries positions for <em>every</em> exercise of the lesson,
/// not just the moved ones: a subset cannot be validated against collisions with the rows it does
/// not mention, and a full list makes the resulting order a single decidable fact.
/// </para>
/// </summary>
public record ReorderExercisesRequestDto(
    List<ExerciseOrderDto> Exercises
);

/// <summary>
/// One exercise's new position within its lesson. Positions must be distinct across the request, but
/// deliberately need not be contiguous or zero-based: existing lessons were authored with whatever
/// numbering their import used, and reads order by this column rather than index into it, so
/// demanding a particular numbering would reject honest reorder requests for no gain.
/// </summary>
public record ExerciseOrderDto(
    Guid ExerciseId,
    int OrderInLesson
);
