namespace Sellevate.Ai.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.27. One lesson's worth of generated exercises. The caller turns it into a real
/// <c>Lesson</c> with real <c>Exercise</c> rows and a frozen <c>LessonVersion</c>
/// (docs/TENANCY/CONTENT_MODEL.md §2.1); this service writes nothing.
/// </summary>
public sealed record GeneratedLessonDto(string Title, IReadOnlyList<GeneratedExerciseDto> Exercises);
