namespace Sellevate.Learning.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.27. A run in a list. Carries neither the material nor the structure: both are documents,
/// and a list of ten runs that ships ten pasted-in product decks is a list nobody loads twice.
/// </summary>
public sealed record ContentGenerationJobSummaryDto(
    Guid Id,
    string Title,
    string Status,
    Guid? ProducedLessonId,
    int ProducedExerciseCount,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime UpdatedAt);
