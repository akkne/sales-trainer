namespace Sellevate.Ai.Features.ContentAdaptation.Models;

/// <summary>
/// Phase 40.32. The verdict on one hand-written exercise: a list, possibly empty.
///
/// <para>
/// <b>Empty is the expected answer and must stay cheap to give.</b> A reviewer that always finds
/// something is a reviewer nobody believes by the tenth exercise, which is precisely how content
/// review stops being quality control and becomes noise the customer clicks past.
/// </para>
/// </summary>
public sealed record ExerciseReviewDto(IReadOnlyList<ExerciseReviewFindingDto> Findings);
