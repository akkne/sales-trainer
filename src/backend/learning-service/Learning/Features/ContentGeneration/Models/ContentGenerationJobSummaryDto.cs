namespace Sellevate.Learning.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.27. A run in a list. Carries neither the material nor the structure: both are documents,
/// and a list of ten runs that ships ten pasted-in product decks is a list nobody loads twice.
///
/// <para>
/// Phase 40.28's refusal <i>is</i> carried, unlike those two. It is half a dozen short sentences, it
/// is the reason the run is sitting there, and a list that shows «insufficient» without saying what
/// is missing sends the administrator into a detail screen to find out — for every refused run.
/// </para>
/// </summary>
/// <param name="GapSourceRef">
/// Phase 40.31. Non-null when the dashboard started this run rather than a person: it names the
/// funnel stage the team was failing and the day that was measured.
/// </param>
public sealed record ContentGenerationJobSummaryDto(
    Guid Id,
    string Title,
    string Status,
    string? GapSourceRef,
    ContentInsufficiencyDto? Insufficiency,
    Guid? ProducedLessonId,
    int ProducedExerciseCount,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime UpdatedAt);
